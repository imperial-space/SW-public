namespace Content.Shared.Imperial.Medieval.Knowledge;

/// <summary>Book activity times at the reader's current intelligence, shared with the book UI.</summary>
public static class BookReadingTime
{
    public static TimeSpan Duration(LearnableBookComponent book, int tier, int intelligence, bool translation = false)
    {
        var seconds = translation ? book.TranslationSeconds : book.StudySeconds;
        if (seconds <= 0)
            seconds = translation
                ? tier switch { <= 1 => 30, 2 => 45, 3 => 60, _ => 90 }
                : tier switch { <= 1 => 25, 2 => 40, 3 => 55, _ => 75 };
        // Ten is the ordinary skill level. Even an exceptional reader needs at least half the base time.
        return TimeSpan.FromSeconds(seconds * 10d / Math.Clamp(intelligence, 5, 20));
    }
}
