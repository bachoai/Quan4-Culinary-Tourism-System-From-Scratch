namespace Quan4CulinaryTourism.Api.Helpers;

public static class EnvFileLoader
{
    public static void LoadIfPresent(params string[] candidatePaths)
    {
        foreach (var candidatePath in candidatePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            LoadFile(candidatePath);
            return;
        }
    }

    private static void LoadFile(string filePath)
    {
        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2)
            {
                var first = value[0];
                var last = value[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    value = value[1..^1];
                }
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
