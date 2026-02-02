using System.Linq;

namespace Content.Server.Imperial.Medieval.Procedural;

public sealed partial class DungeonGenerationSystem
{
    // Структура данных
    public struct DungeonLayout
    {
        public bool[,] HorizontalDoors;
        public bool[,] VerticalDoors;
        public List<Vector2i> StartRooms;
        public List<(Vector2i Room, Vector2i DoorIdx, bool IsHor)> SpecialPairs;
    }

    private DungeonLayout GenerateLogicalLayout(int width, int height, Random random)
    {
        var layout = new DungeonLayout
        {
            HorizontalDoors = new bool[width - 1, height],
            VerticalDoors = new bool[width, height - 1],
            StartRooms = new List<Vector2i>(),
            SpecialPairs = new List<(Vector2i Room, Vector2i DoorIdx, bool IsHor)>()
        };

        GenerateMaze(width, height, random, ref layout);
        AddLoops(width, height, random, ref layout);
        ChooseStartRooms(width, height, random, ref layout);
        FindSpecialPairs(width, height, random, ref layout);

        return layout;
    }

    private void GenerateMaze(int width, int height, Random random, ref DungeonLayout layout)
    {
        var visited = new bool[width, height];
        var stack = new Stack<Vector2i>();
        stack.Push(new Vector2i(0, 0));
        visited[0, 0] = true;

        var directions = new Vector2i[] { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var unvisited = new List<Vector2i>();
            foreach (var dir in directions)
            {
                var n = current + dir;
                if (n.X >= 0 && n.X < width && n.Y >= 0 && n.Y < height && !visited[n.X, n.Y])
                    unvisited.Add(n);
            }

            if (unvisited.Count > 0)
            {
                var next = unvisited[random.Next(unvisited.Count)];
                if (next.X > current.X) layout.HorizontalDoors[current.X, current.Y] = true;
                else if (next.X < current.X) layout.HorizontalDoors[next.X, current.Y] = true;
                else if (next.Y > current.Y) layout.VerticalDoors[current.X, current.Y] = true;
                else if (next.Y < current.Y) layout.VerticalDoors[current.X, next.Y] = true;

                visited[next.X, next.Y] = true;
                stack.Push(next);
            }
            else stack.Pop();
        }
    }

    private void AddLoops(int width, int height, Random random, ref DungeonLayout layout)
    {
        var potentialWalls = new List<(Vector2i CellA, Vector2i CellB, bool IsHorizontal)>();

        for (var x = 0; x < width - 1; x++)
            for (var y = 0; y < height; y++)
                if (!layout.HorizontalDoors[x, y])
                    potentialWalls.Add((new Vector2i(x, y), new Vector2i(x + 1, y), true));

        for (var x = 0; x < width; x++)
            for (var y = 0; y < height - 1; y++)
                if (!layout.VerticalDoors[x, y])
                    potentialWalls.Add((new Vector2i(x, y), new Vector2i(x, y + 1), false));

        var walls = potentialWalls.OrderBy(_ => random.Next()).ToList();

        foreach (var (a, b, isHor) in walls)
        {
            if (CountDoors(a, width, height, layout) >= 3 || CountDoors(b, width, height, layout) >= 3)
                continue;

            if (random.NextDouble() < 0.20)
            {
                if (isHor) layout.HorizontalDoors[Math.Min(a.X, b.X), a.Y] = true;
                else layout.VerticalDoors[a.X, Math.Min(a.Y, b.Y)] = true;
            }
        }
    }

    private int CountDoors(Vector2i cell, int w, int h, DungeonLayout layout)
    {
        int count = 0;
        if (cell.X > 0 && layout.HorizontalDoors[cell.X - 1, cell.Y]) count++;
        if (cell.X < w - 1 && layout.HorizontalDoors[cell.X, cell.Y]) count++;
        if (cell.Y > 0 && layout.VerticalDoors[cell.X, cell.Y - 1]) count++;
        if (cell.Y < h - 1 && layout.VerticalDoors[cell.X, cell.Y]) count++;
        return count;
    }

    private void ChooseStartRooms(int width, int height, Random random, ref DungeonLayout layout)
    {
        var allRooms = new List<Vector2i>();
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                allRooms.Add(new Vector2i(x, y));

        for (int i = 0; i < 4 && allRooms.Count > 0; i++)
        {
            var idx = random.Next(allRooms.Count);
            layout.StartRooms.Add(allRooms[idx]);
            allRooms.RemoveAt(idx);
        }
    }

    private void FindSpecialPairs(int width, int height, Random random, ref DungeonLayout layout)
    {
        var targetCount = width * height / 5;
        if (targetCount < 1) targetCount = 1;

        var candidateDoors = new List<(Vector2i Idx, bool IsHor)>();
        for (var x = 0; x < width - 1; x++)
            for (var y = 0; y < height; y++)
                if (layout.HorizontalDoors[x, y]) candidateDoors.Add((new Vector2i(x, y), true));

        for (var x = 0; x < width; x++)
            for (var y = 0; y < height - 1; y++)
                if (layout.VerticalDoors[x, y]) candidateDoors.Add((new Vector2i(x, y), false));

        candidateDoors = candidateDoors.OrderBy(_ => random.Next()).ToList();
        var lockedDoorsSet = new HashSet<(Vector2i, bool)>();

        foreach (var (doorIdx, isHor) in candidateDoors)
        {
            if (layout.SpecialPairs.Count >= targetCount) break;

            lockedDoorsSet.Add((doorIdx, isHor));
            var reachableRooms = GetReachableRooms(layout.StartRooms, lockedDoorsSet, width, height, layout);

            if (reachableRooms.Count > 0)
            {
                var triggerRoom = reachableRooms[random.Next(reachableRooms.Count)];
                layout.SpecialPairs.Add((triggerRoom, doorIdx, isHor));
            }
            else
            {
                lockedDoorsSet.Remove((doorIdx, isHor));
            }
        }
    }

    private List<Vector2i> GetReachableRooms(List<Vector2i> starts, HashSet<(Vector2i, bool)> lockedDoors, int w, int h, DungeonLayout layout)
    {
        var visited = new HashSet<Vector2i>();
        var queue = new Queue<Vector2i>();
        var result = new List<Vector2i>();

        foreach (var start in starts)
        {
            if (visited.Add(start))
            {
                queue.Enqueue(start);
                result.Add(start);
            }
        }

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();

            if (curr.Y < h - 1 && layout.VerticalDoors[curr.X, curr.Y] && !lockedDoors.Contains((new Vector2i(curr.X, curr.Y), false)))
            {
                var next = new Vector2i(curr.X, curr.Y + 1);
                if (visited.Add(next)) { queue.Enqueue(next); result.Add(next); }
            }
            if (curr.Y > 0 && layout.VerticalDoors[curr.X, curr.Y - 1] && !lockedDoors.Contains((new Vector2i(curr.X, curr.Y - 1), false)))
            {
                var next = new Vector2i(curr.X, curr.Y - 1);
                if (visited.Add(next)) { queue.Enqueue(next); result.Add(next); }
            }
            if (curr.X < w - 1 && layout.HorizontalDoors[curr.X, curr.Y] && !lockedDoors.Contains((new Vector2i(curr.X, curr.Y), true)))
            {
                var next = new Vector2i(curr.X + 1, curr.Y);
                if (visited.Add(next)) { queue.Enqueue(next); result.Add(next); }
            }
            if (curr.X > 0 && layout.HorizontalDoors[curr.X - 1, curr.Y] && !lockedDoors.Contains((new Vector2i(curr.X - 1, curr.Y), true)))
            {
                var next = new Vector2i(curr.X - 1, curr.Y);
                if (visited.Add(next)) { queue.Enqueue(next); result.Add(next); }
            }
        }
        return result;
    }
}
