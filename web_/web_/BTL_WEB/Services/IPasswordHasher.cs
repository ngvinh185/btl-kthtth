namespace BTL_WEB.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string inputPassword, string storedPassword);
}
