namespace NetStucked.Infrastructure;
public static class AtomicFileReplace
{
    public static async Task MoveAsync(string temporary, string destination, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { File.Move(temporary, destination, true); return; }
            catch (Exception ex) when (attempt < 3 && ex is IOException or UnauthorizedAccessException) { await Task.Delay(100 << attempt, token); }
        }
    }
}
