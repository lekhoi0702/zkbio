using ZkbioDashboard.Models;

namespace ZkbioDashboard.Services;

public interface IAuthService
{
    Task<User?> Authenticate(string username, string password);
}

public class AuthService : IAuthService
{
    private readonly string _csvPath;

    public AuthService(IWebHostEnvironment env)
    {
        _csvPath = Path.Combine(env.ContentRootPath, "users.csv");
    }

    public async Task<User?> Authenticate(string username, string password)
    {
        if (!File.Exists(_csvPath)) return null;

        var lines = await File.ReadAllLinesAsync(_csvPath);
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length == 2)
            {
                var storedUsername = parts[0].Trim();
                var storedPassword = parts[1].Trim();

                if (storedUsername == username && storedPassword == password)
                {
                    return new User { Username = storedUsername, Password = storedPassword };
                }
            }
        }

        return null;
    }
}
