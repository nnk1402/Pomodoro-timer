using System.IO;
using System.Text.Json;

namespace Pomodoro.Features.Todo;

internal static class TodoStore
{
    internal static string StoragePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pomodoro", "todos.json");

    internal static IReadOnlyList<TodoData> Load(string path, out string error)
    {
        error = "";
        try
        {
            if (!File.Exists(path)) return [];
            var entries = JsonSerializer.Deserialize<List<TodoData>>(File.ReadAllText(path));
            if (entries is null || entries.Any(item => item is null || string.IsNullOrWhiteSpace(item.Title)))
                throw new JsonException();
            return entries;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            error = "Chưa đọc được danh sách đã lưu. Hãy kiểm tra tệp todos.json trước khi tạo danh sách mới.";
            return [];
        }
    }

    internal static bool Save(IEnumerable<TodoData> entries, string path, out string error)
    {
        error = "";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = "Chưa lưu được danh sách. Thay đổi vẫn còn ở đây; hãy kiểm tra quyền ghi hoặc dung lượng rồi thử lưu lại.";
            return false;
        }
    }
}
