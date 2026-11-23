namespace SNextSeafood.Web.Helpers;

/// <summary>
/// Tự động nạp các biến môi trường từ file .env ở thư mục gốc Solution hoặc thư mục chạy ứng dụng
/// trước khi khởi tạo WebApplicationBuilder (không cần thư viện bên thứ ba).
/// </summary>
public static class EnvFileLoader
{
    public static void LoadFromSolutionRoot()
    {
        var candidatePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env")
        };

        var envFilePath = candidatePaths
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);

        if (envFilePath is null)
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(envFilePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            // Không ghi đè biến môi trường đã được thiết lập sẵn ở hệ điều hành / CI / Docker
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
