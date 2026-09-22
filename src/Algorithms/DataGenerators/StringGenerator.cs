namespace Algorithms.DataGenerators;

public static class StringGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

    public static string GenerateRandomString(int length, Random? rng = null)
    {
        rng ??= new Random();
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = Alphabet[rng.Next(Alphabet.Length)];
        return new string(chars);
    }

    public static (string text, string pattern) GenerateStringWithPattern(int textLength, string pattern, Random? rng = null)
    {
        rng ??= new Random();

        // Генерируем базовую строку
        var text = GenerateRandomString(textLength, rng);

        // Вставляем паттерн в несколько случайных позиций
        int insertions = rng.Next(1, 5); // от 1 до 4 вставок
        for (int i = 0; i < insertions; i++)
        {
            int pos = rng.Next(0, text.Length);
            text = text.Insert(pos, pattern);
        }

        return (text, pattern);
    }
}