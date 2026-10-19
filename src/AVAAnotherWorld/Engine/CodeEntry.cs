namespace AVAAnotherWorld.Engine;

/// <summary>
/// The four letters of a checkpoint's code, one under the cursor at a time. Letters can be typed straight in or dialled
/// up and down through the alphabet (with a gamepad).
/// </summary>
public sealed class CodeEntry
{
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const int Length = 4;

    private readonly char[] _letters = "AAAA".ToCharArray();

    public int Cursor { get; private set; }
    public bool IsDone { get; private set; }
    /// <summary>How many letters have been put in so far.</summary>
    public int Filled { get; private set; }
    public string Code => new(_letters);

    /// <summary>Steps the letter under the cursor through the alphabet, wrapping at either end.</summary>
    public void Cycle(int direction)
    {
        int index = Alphabet.IndexOf(_letters[Cursor]);
        index = (index + direction % Alphabet.Length + Alphabet.Length) % Alphabet.Length;
        _letters[Cursor] = Alphabet[index];
        Filled = Math.Max(Filled, Cursor + 1);
    }

    /// <summary>Sets the letter under the cursor and moves on, as typing does; the fourth finishes the code.</summary>
    public void Type(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (Alphabet.IndexOf(letter) < 0) return;
        _letters[Cursor] = letter;
        Filled = Math.Max(Filled, Cursor + 1);
        Advance();
    }

    /// <summary>Accepts the letter under the cursor; accepting the last one finishes the code.</summary>
    public void Advance()
    {
        if (Cursor < Length - 1) Cursor++;
        else IsDone = true;
    }

    /// <summary>Back a letter, as Backspace does.</summary>
    public void Back() => Cursor = Math.Max(0, Cursor - 1);

    public void Finish() => IsDone = true;
}
