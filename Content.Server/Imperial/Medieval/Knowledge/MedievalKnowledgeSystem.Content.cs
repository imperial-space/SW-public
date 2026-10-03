using System.Text;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Paper;
using Content.Shared.UserInterface;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Knowledge;

public sealed partial class MedievalKnowledgeSystem
{
    private void OnBookOpened(Entity<LearnableBookComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        RefreshBookContent(ent);
    }

    private void OpenBook(EntityUid reader, Entity<LearnableBookComponent> book)
    {
        if (!TryComp<PaperComponent>(book, out var paper))
            return;
        paper.Mode = PaperComponent.PaperAction.Read;
        RefreshBookContent(book);
        _ui.TryOpenUi(book.Owner, PaperComponent.PaperUiKey.Key, reader);
        // The destination's writing editor may already be open behind the original's pages.
        _ui.ServerSendUiMessage(book.Owner, PaperComponent.PaperUiKey.Key, new FocusKnowledgeBookMessage(), reader);
    }

    /// <summary>
    /// Physical pages are authoritative: encrypted originals never contain readable knowledge.
    /// Regeneration also supports rare loot whose knowledge is chosen after spawning.
    /// </summary>
    public void RefreshBookContent(Entity<LearnableBookComponent> book)
    {
        if (!TryComp<PaperComponent>(book, out var paper))
            return;
        paper.EditingDisabled = true;
        var content = BuildBookContent(book.Comp);
        _paper.SetContent((book, paper), content);
    }

    public string BuildBookContent(LearnableBookComponent book)
    {
        if (book.Encrypted)
            return Loc.GetString("knowledge-cipher-heading") + "\n\n" + CipherPage(book.Knowledge, book.Language);
        if (!_prototypes.TryIndex<MedievalKnowledgePrototype>(book.Knowledge, out var knowledge))
            return string.Empty;

        var text = new StringBuilder();
        text.AppendLine(book.Title ?? Loc.GetString(knowledge.BookTitle));
        text.AppendLine();
        text.AppendLine(book.Text ?? Loc.GetString(knowledge.BookText));
        if (book.Translator != null)
        {
            text.AppendLine();
            text.AppendLine(Loc.GetString("knowledge-edition-author", ("author", FormattedMessage.EscapeText(book.Translator))));
        }
        return text.ToString().TrimEnd();
    }

    private static string CipherPage(string knowledge, string language)
    {
        const string glyphs = "λξψφΩΔЖѰФϟΘΣѮЮ";
        // FNV seed avoids per-process string hashing and keeps the same edition legible only as cipher.
        uint seed = 2166136261;
        foreach (var character in knowledge + ":" + language)
            seed = unchecked((seed ^ character) * 16777619);
        var text = new StringBuilder();
        for (var line = 0; line < 10; line++)
        {
            for (var column = 0; column < 36; column++)
            {
                seed = unchecked(seed * 1664525 + 1013904223);
                text.Append(column % 6 == 5 ? ' ' : glyphs[(int) (seed % glyphs.Length)]);
            }
            text.AppendLine();
        }
        return text.ToString().TrimEnd();
    }
}
