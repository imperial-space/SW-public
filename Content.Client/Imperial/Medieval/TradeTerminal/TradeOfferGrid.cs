using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Hands.Systems;
using Content.Client.Items.Systems;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Content.Shared.Input;
using Content.Shared.Item;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Trade;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Trade.UI;

public sealed class TradeOfferGrid : PanelContainer
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private static readonly Color BaseCellColor = Color.FromHex("#8e7348");
    private static readonly Angle RotatedInsertionAngle = Angle.FromDegrees(90);

    private readonly Control _layerRoot;
    private readonly GridContainer _backgroundGrid;
    private readonly GridContainer _pieceGrid;
    private readonly List<TradeOfferCell> _cells = new();
    private readonly List<TextureRect> _backgroundCells = new();

    private readonly string _emptyTexturePath = "Storage/tile_empty";
    private Texture? _emptyTexture;
    private Vector2i _gridSize;
    private Vector2i? _hoveredCell;
    private bool _canInsert;
    private Angle _insertRotation = Angle.Zero;

    public event Action<ItemStorageLocation, int>? OnInsertAt;
    public event Action<NetEntity>? OnRemoveItem;

    public TradeOfferGrid()
    {
        IoCManager.InjectDependencies(this);

        _backgroundGrid = new GridContainer
        {
            HSeparationOverride = 0,
            VSeparationOverride = 0,
        };

        _pieceGrid = new GridContainer
        {
            HSeparationOverride = 0,
            VSeparationOverride = 0,
        };

        _layerRoot = new Control
        {
            Children =
            {
                _backgroundGrid,
                _pieceGrid,
            },
        };

        AddChild(_layerRoot);
        OnThemeUpdated();
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();
        _emptyTexture = Theme.ResolveTextureOrNull(_emptyTexturePath)?.Texture;
        _gridSize = Vector2i.Zero;
    }

    public void SetStorageEntity(EntityUid storageEntity)
    {
        // Trade terminal uses its own trade UI and only mirrors the underlying storage grid.
    }

    public void UpdateOffer(TradeOfferGridDto grid, List<TradeItemDto>? items, bool canInsert, bool canRemove)
    {
        _canInsert = canInsert;
        if (!canInsert)
        {
            _hoveredCell = null;
            _insertRotation = Angle.Zero;
        }

        BuildBackground(grid);
        BuildCells(grid, canInsert);
        ResetCellState(canInsert, canRemove);
        ClearPieces();

        if (items == null)
            return;

        foreach (var item in items.OrderBy(i => i.StorageLocation.Position.Y).ThenBy(i => i.StorageLocation.Position.X))
        {
            AddItem(item, canRemove);
        }
    }

    private void BuildBackground(TradeOfferGridDto grid)
    {
        var cellSize = GetCellSize();
        var gridPixelSize = new Vector2(cellSize.X * grid.Width, cellSize.Y * grid.Height);

        MinSize = gridPixelSize;
        SetSize = gridPixelSize;
        _layerRoot.MinSize = gridPixelSize;
        _layerRoot.SetSize = gridPixelSize;
        _backgroundGrid.MinSize = gridPixelSize;
        _backgroundGrid.SetSize = gridPixelSize;
        _pieceGrid.MinSize = gridPixelSize;
        _pieceGrid.SetSize = gridPixelSize;

        if (_gridSize.X == grid.Width &&
            _gridSize.Y == grid.Height &&
            _backgroundGrid.ChildCount == grid.Width * grid.Height)
        {
            return;
        }

        _backgroundGrid.RemoveAllChildren();
        _backgroundCells.Clear();
        _backgroundGrid.Rows = grid.Height;
        _backgroundGrid.Columns = grid.Width;

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var textureRect = new TextureRect
                {
                    Texture = _emptyTexture,
                    TextureScale = new Vector2(2, 2),
                    ModulateSelfOverride = BaseCellColor,
                    MinSize = cellSize,
                };

                _backgroundGrid.AddChild(textureRect);
                _backgroundCells.Add(textureRect);
            }
        }
    }

    private void BuildCells(TradeOfferGridDto grid, bool canInsert)
    {
        var cellSize = GetCellSize();

        if (_gridSize.X != grid.Width || _gridSize.Y != grid.Height)
        {
            _pieceGrid.RemoveAllChildren();
            _pieceGrid.Rows = grid.Height;
            _pieceGrid.Columns = grid.Width;
            _cells.Clear();

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new TradeOfferCell
                    {
                        GridX = x,
                        GridY = y,
                        MinSize = cellSize,
                        AcceptsInsert = canInsert,
                    };

                    cell.PrimaryPressed += OnCellPrimaryPressed;
                    cell.SecondaryPressed += OnCellSecondaryPressed;
                    cell.HoverEntered += OnCellHoverEntered;
                    cell.HoverExited += OnCellHoverExited;
                    cell.RotatePressed += OnCellRotatePressed;
                    _cells.Add(cell);
                    _pieceGrid.AddChild(cell);
                }
            }

            _gridSize = new Vector2i(grid.Width, grid.Height);
            return;
        }

        foreach (var cell in _cells)
        {
            cell.AcceptsInsert = canInsert;
            cell.MinSize = cellSize;
        }
    }

    private void ResetCellState(bool canInsert, bool canRemove)
    {
        foreach (var cell in _cells)
        {
            cell.SetEmpty(canInsert, canRemove);
        }
    }

    private void ClearPieces()
    {
        foreach (var cell in _cells)
        {
            cell.RemoveAllChildren();
        }
    }

    private void AddItem(TradeItemDto item, bool canRemove)
    {
        if (_gridSize.X <= 0 || _gridSize.Y <= 0)
            return;

        var position = item.StorageLocation.Position;
        if (!TryGetCell(position.X, position.Y, out var cell))
            return;

        RegisterOccupiedCells(item, canRemove);

        var localUid = _entityManager.GetEntity(item.Entity);
        if (localUid != EntityUid.Invalid &&
            _entityManager.EntityExists(localUid) &&
            _entityManager.TryGetComponent<ItemComponent>(localUid, out _))
        {
            cell.AddChild(new TradeItemSlotControl(localUid, item, GetCellSize(), _entityManager));
            return;
        }

        cell.AddChild(new TradeFallbackItemControl(item, GetCellSize()));
    }

    private void RegisterOccupiedCells(TradeItemDto item, bool canRemove)
    {
        for (var y = 0; y < item.GridHeight; y++)
        {
            for (var x = 0; x < item.GridWidth; x++)
            {
                var cellPos = item.StorageLocation.Position + new Vector2i(x, y);
                if (cellPos.X < 0 || cellPos.Y < 0 || cellPos.X >= _gridSize.X || cellPos.Y >= _gridSize.Y)
                    continue;

                _cells[cellPos.X + cellPos.Y * _gridSize.X].SetOccupied(item, canRemove);
            }
        }
    }

    private void OnCellPrimaryPressed(int x, int y)
    {
        if (!TryGetCell(x, y, out var cell))
            return;

        var location = new ItemStorageLocation(_insertRotation, new Vector2i(x, y));

        if (cell.OccupiedItem != null)
        {
            if (TryGetHeldItem(out var heldItem) && CanInsertIntoCell(heldItem, cell))
            {
                OnInsertAt?.Invoke(location, 0);
                return;
            }

            if (cell.CanRemoveOccupied)
                OnRemoveItem?.Invoke(cell.OccupiedItem.Entity);

            return;
        }

        if (!cell.AcceptsInsert ||
            !TryGetHeldItem(out var emptyCellHeldItem) ||
            !CanInsertIntoGrid(emptyCellHeldItem, new Vector2i(x, y), GetItemGridSize(emptyCellHeldItem, _insertRotation)))
        {
            return;
        }

        OnInsertAt?.Invoke(location, 0);
    }

    private void OnCellSecondaryPressed(int x, int y)
    {
        if (!TryGetCell(x, y, out var cell) ||
            !cell.AcceptsInsert ||
            !TryGetHeldItem(out var heldItem))
        {
            return;
        }

        if (cell.OccupiedItem != null)
        {
            if (!CanInsertIntoCell(heldItem, cell))
                return;
        }
        else if (!CanInsertIntoGrid(heldItem, new Vector2i(x, y), GetItemGridSize(heldItem, _insertRotation)))
        {
            return;
        }

        OnInsertAt?.Invoke(new ItemStorageLocation(_insertRotation, new Vector2i(x, y)), 1);
    }

    private void OnCellHoverEntered(int x, int y)
    {
        _hoveredCell = new Vector2i(x, y);
    }

    private void OnCellHoverExited(int x, int y)
    {
        if (_hoveredCell == new Vector2i(x, y))
            _hoveredCell = null;
    }

    private void OnCellRotatePressed()
    {
        _insertRotation = _insertRotation == Angle.Zero
            ? RotatedInsertionAngle
            : Angle.Zero;
    }

    private Vector2 GetCellSize()
    {
        if (_emptyTexture == null)
            return new Vector2(32, 32);

        return _emptyTexture.Size * 2;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateInsertionPreview();
    }

    private void UpdateInsertionPreview()
    {
        foreach (var cell in _backgroundCells)
        {
            cell.ModulateSelfOverride = BaseCellColor;
        }

        if (!_canInsert ||
            _hoveredCell is not { } hoveredCell ||
            !TryGetHeldItem(out var heldItem))
        {
            return;
        }

        var heldSize = GetItemGridSize(heldItem, _insertRotation);
        var previewColor = CanInsertIntoGrid(heldItem, hoveredCell, heldSize)
            ? Color.Goldenrod
            : Color.FromHex("#B40046");

        for (var y = 0; y < heldSize.Y; y++)
        {
            for (var x = 0; x < heldSize.X; x++)
            {
                if (!TryGetBackgroundCell(hoveredCell.X + x, hoveredCell.Y + y, out var backgroundCell))
                    continue;

                backgroundCell.ModulateSelfOverride = previewColor;
            }
        }
    }

    private bool TryGetHeldItem(out EntityUid heldItem)
    {
        heldItem = EntityUid.Invalid;

        var handsSystem = _entityManager.System<HandsSystem>();
        if (handsSystem.GetActiveHandEntity() is not { } activeItem)
            return false;

        heldItem = activeItem;
        return true;
    }

    private bool CanInsertIntoCell(EntityUid heldItem, TradeOfferCell cell)
    {
        if (cell.OccupiedItem == null)
            return true;

        var target = _entityManager.GetEntity(cell.OccupiedItem.Entity);
        return target != EntityUid.Invalid &&
               _entityManager.TryGetComponent<StackComponent>(heldItem, out var heldStack) &&
               _entityManager.TryGetComponent<StackComponent>(target, out var targetStack) &&
               heldStack.StackTypeId == targetStack.StackTypeId;
    }

    private Vector2i GetItemGridSize(EntityUid item, Angle rotation)
    {
        if (!_entityManager.TryGetComponent<ItemComponent>(item, out var itemComp))
            return new Vector2i(1, 1);

        var bounds = _entityManager.System<ItemSystem>()
            .GetAdjustedItemShape((item, itemComp), rotation, Vector2i.Zero)
            .GetBoundingBox();

        return new Vector2i(Math.Max(1, bounds.Width + 1), Math.Max(1, bounds.Height + 1));
    }

    private bool CanInsertIntoGrid(EntityUid heldItem, Vector2i origin, Vector2i size)
    {
        if (origin.X < 0 ||
            origin.Y < 0 ||
            origin.X + size.X > _gridSize.X ||
            origin.Y + size.Y > _gridSize.Y)
        {
            return false;
        }

        TradeItemDto? occupiedItem = null;

        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                if (!TryGetCell(origin.X + x, origin.Y + y, out var cell))
                    return false;

                if (cell.OccupiedItem == null)
                    continue;

                if (occupiedItem == null)
                {
                    occupiedItem = cell.OccupiedItem;
                    continue;
                }

                if (occupiedItem.Entity != cell.OccupiedItem.Entity)
                    return false;
            }
        }

        if (occupiedItem == null)
            return true;

        return TryGetCell(origin.X, origin.Y, out var occupiedCell) &&
               CanInsertIntoCell(heldItem, occupiedCell);
    }

    private bool TryGetCell(int x, int y, [NotNullWhen(true)] out TradeOfferCell? cell)
    {
        cell = null;

        if (x < 0 || y < 0 || x >= _gridSize.X || y >= _gridSize.Y)
            return false;

        var index = x + y * _gridSize.X;
        if (index < 0 || index >= _cells.Count)
            return false;

        cell = _cells[index];
        return true;
    }

    private bool TryGetBackgroundCell(int x, int y, [NotNullWhen(true)] out TextureRect? cell)
    {
        cell = null;

        if (x < 0 || y < 0 || x >= _gridSize.X || y >= _gridSize.Y)
            return false;

        var index = x + y * _gridSize.X;
        if (index < 0 || index >= _backgroundCells.Count)
            return false;

        cell = _backgroundCells[index];
        return true;
    }

    private sealed class TradeOfferCell : PanelContainer
    {
        public int GridX;
        public int GridY;
        public bool AcceptsInsert;
        public bool CanRemoveOccupied;
        public TradeItemDto? OccupiedItem;

        private bool _hovered;

        public event Action<int, int>? PrimaryPressed;
        public event Action<int, int>? SecondaryPressed;
        public event Action<int, int>? HoverEntered;
        public event Action<int, int>? HoverExited;
        public event Action? RotatePressed;

        public TradeOfferCell()
        {
            MouseFilter = MouseFilterMode.Stop;
        }

        public void SetEmpty(bool acceptsInsert, bool canRemoveOccupied)
        {
            OccupiedItem = null;
            AcceptsInsert = acceptsInsert;
            CanRemoveOccupied = canRemoveOccupied;
            TooltipSupplier = null;
            ToolTip = acceptsInsert ? Loc.GetString("trade-terminal-insert-hint") : null;
            UpdateStyle();
        }

        public void SetOccupied(TradeItemDto item, bool canRemove)
        {
            OccupiedItem = item;
            CanRemoveOccupied = canRemove;
            ToolTip = null;
            TooltipSupplier = _ => BuildItemTooltip(item, canRemove);
            UpdateStyle();
        }

        protected override void MouseEntered()
        {
            base.MouseEntered();
            _hovered = true;
            HoverEntered?.Invoke(GridX, GridY);
            UpdateStyle();
        }

        protected override void MouseExited()
        {
            base.MouseExited();
            _hovered = false;
            HoverExited?.Invoke(GridX, GridY);
            UpdateStyle();
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);

            if (args.Handled)
                return;

            if (args.Function == ContentKeyFunctions.RotateStoredItem)
            {
                if (!AcceptsInsert)
                    return;

                RotatePressed?.Invoke();
                args.Handle();
                return;
            }

            if (args.Function == EngineKeyFunctions.UIRightClick)
            {
                SecondaryPressed?.Invoke(GridX, GridY);
                args.Handle();
                return;
            }

            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            PrimaryPressed?.Invoke(GridX, GridY);
            args.Handle();
        }

        private void UpdateStyle()
        {
            if (OccupiedItem != null)
            {
                PanelOverride = MakeCellStyle(
                    _hovered ? "#7e622d5c" : "#2c20110e",
                    CanRemoveOccupied
                        ? (_hovered ? "#f2cf88" : "#bf9557")
                        : (_hovered ? "#8a7859" : "#64553e"),
                    _hovered ? 2 : 1);
                return;
            }

            if (AcceptsInsert)
            {
                PanelOverride = MakeCellStyle(
                    _hovered ? "#90712f3f" : "#20170d08",
                    _hovered ? "#eec97b" : "#846743",
                    _hovered ? 2 : 1);
                return;
            }

            PanelOverride = MakeCellStyle("#16110d08", "#4d403050", 1);
        }

        private static StyleBoxFlat MakeCellStyle(string background, string border, int thickness)
        {
            return new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex(background),
                BorderColor = Color.FromHex(border),
                BorderThickness = new Thickness(thickness),
            };
        }

        private static Control BuildItemTooltip(TradeItemDto item, bool canRemove)
        {
            var content = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 2,
            };

            content.AddChild(new Label
            {
                Text = item.Name,
                ModulateSelfOverride = Color.FromHex("#f3cc7f"),
            });

            if (item.StackCount is > 1)
            {
                content.AddChild(new Label
                {
                    Text = $"{Loc.GetString("cargo-console-order-menu-amount-label")} {item.StackCount.Value}",
                    ModulateSelfOverride = Color.FromHex("#f6e4ba"),
                });
            }

            if (!string.IsNullOrWhiteSpace(item.Description) && item.Description != item.Name)
            {
                content.AddChild(new Label
                {
                    Text = item.Description,
                    ModulateSelfOverride = Color.FromHex("#c6af8a"),
                });
            }

            if (canRemove)
            {
                content.AddChild(new Label
                {
                    Text = Loc.GetString("trade-terminal-btn-remove-item-tooltip"),
                    ModulateSelfOverride = Color.FromHex("#d5c29c"),
                });
            }

            var panel = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#22160df0"),
                    BorderColor = Color.FromHex("#c89b4f"),
                    BorderThickness = new Thickness(1),
                    ContentMarginLeftOverride = 7,
                    ContentMarginTopOverride = 5,
                    ContentMarginRightOverride = 7,
                    ContentMarginBottomOverride = 5,
                },
                Children = { content },
            };

            var tooltip = new Tooltip();
            tooltip.GetChild(0).Children.Clear();
            tooltip.GetChild(0).Children.Add(panel);
            return tooltip;
        }
    }

    private abstract class TradePieceOverlay : Control
    {
        private readonly Vector2 _cellSize;

        protected TradePieceOverlay(Vector2 cellSize)
        {
            _cellSize = cellSize;
            MinSize = cellSize;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            foreach (var child in Children)
            {
                child.Measure(availableSize);
            }

            return _cellSize;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            foreach (var child in Children)
            {
                child.Arrange(UIBox2.FromDimensions(Vector2.Zero, child.DesiredSize));
            }

            return finalSize;
        }
    }

    private sealed class TradeFallbackItemControl : TradePieceOverlay
    {
        public TradeFallbackItemControl(TradeItemDto item, Vector2 cellSize)
            : base(cellSize)
        {
            var itemPixelSize = new Vector2(cellSize.X * item.GridWidth, cellSize.Y * item.GridHeight);
            var panel = new PanelContainer
            {
                MinSize = itemPixelSize,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#5a4527"),
                    BorderColor = Color.FromHex("#ddb56d"),
                    BorderThickness = new Thickness(1),
                },
            };

            var label = new Label
            {
                Text = item.StackCount is > 1
                    ? $"{item.Name} x{item.StackCount.Value}"
                    : item.Name,
                ClipText = true,
                HorizontalExpand = true,
                VerticalExpand = true,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(4),
                ModulateSelfOverride = Color.FromHex("#f1e0bb"),
            };

            panel.AddChild(label);
            AddChild(panel);
        }
    }

    private sealed class TradeItemSlotControl : TradePieceOverlay
    {
        public TradeItemSlotControl(EntityUid entity, TradeItemDto item, Vector2 cellSize, IEntityManager entityManager)
            : base(cellSize)
        {
            if (!entityManager.TryGetComponent<ItemComponent>(entity, out var itemComp))
                return;

            var itemPixelSize = new Vector2(cellSize.X * item.GridWidth, cellSize.Y * item.GridHeight);
            var layout = new LayoutContainer
            {
                MinSize = itemPixelSize,
                MouseFilter = MouseFilterMode.Ignore,
            };

            var piece = new ItemGridPiece((entity, itemComp), item.StorageLocation, entityManager)
            {
                MinSize = cellSize,
                MouseFilter = MouseFilterMode.Ignore,
            };
            layout.AddChild(piece);

            if (item.StackCount is > 1)
            {
                var badge = new PanelContainer
                {
                    MouseFilter = MouseFilterMode.Ignore,
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#25160ccc"),
                        BorderColor = Color.FromHex("#f0cf89"),
                        BorderThickness = new Thickness(1),
                        ContentMarginLeftOverride = 2,
                        ContentMarginTopOverride = 0,
                        ContentMarginRightOverride = 2,
                        ContentMarginBottomOverride = 0,
                    },
                    Children =
                    {
                        new Label
                        {
                            Text = item.StackCount.Value.ToString(),
                            StyleClasses = { "LabelSubText" },
                            HorizontalAlignment = HAlignment.Center,
                            VerticalAlignment = VAlignment.Center,
                            ModulateSelfOverride = Color.FromHex("#fff0c8"),
                        },
                    },
                };

                LayoutContainer.SetAnchorAndMarginPreset(badge, LayoutContainer.LayoutPreset.TopRight, margin: 2);
                LayoutContainer.SetGrowHorizontal(badge, LayoutContainer.GrowDirection.Begin);
                LayoutContainer.SetGrowVertical(badge, LayoutContainer.GrowDirection.Begin);
                layout.AddChild(badge);
            }

            AddChild(layout);
        }
    }
}
