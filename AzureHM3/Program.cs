using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

class AzureBlobService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _containerClient;

    public AzureBlobService(string connectionString, string containerName)
    {
        _serviceClient = new BlobServiceClient(connectionString);
        _containerClient = _serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task CreateContainerAsync()
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        Console.WriteLine($"Контейнер '{_containerClient.Name}' створено або вже існує.");
    }

    public async Task DeleteContainerAsync()
    {
        await _containerClient.DeleteIfExistsAsync();
        Console.WriteLine($"Контейнер '{_containerClient.Name}' видалено.");
    }

    public async Task UploadFileAsync(string localFilePath, string? blobName = null)
    {
        blobName ??= Path.GetFileName(localFilePath);
        var blobClient = _containerClient.GetBlobClient(blobName);
        using var stream = File.OpenRead(localFilePath);
        await blobClient.UploadAsync(stream, overwrite: true);
        Console.WriteLine($"Файл '{blobName}' завантажено.");
    }

    public async Task UploadTextAsync(string blobName, string content)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await blobClient.UploadAsync(stream, overwrite: true);
        Console.WriteLine($"Файл '{blobName}' створено з тексту.");
    }

    public async Task<string> ReadFileAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        var response = await blobClient.DownloadAsync();
        using var reader = new StreamReader(response.Value.Content);
        string content = await reader.ReadToEndAsync();
        Console.WriteLine($"Файл '{blobName}' прочитано.");
        return content;
    }

    public async Task DownloadFileAsync(string blobName, string localPath)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DownloadToAsync(localPath);
        Console.WriteLine($"Файл '{blobName}' збережено як '{localPath}'.");
    }

    public async Task UpdateFileAsync(string blobName, string newContent)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(newContent));
        await blobClient.UploadAsync(stream, overwrite: true);
        Console.WriteLine($"Файл '{blobName}' оновлено.");
    }

    public async Task DeleteFileAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
        Console.WriteLine($"Файл '{blobName}' видалено.");
    }

    public async Task<bool> FileExistsAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        var response = await blobClient.ExistsAsync();
        return response.Value;
    }

    public async Task CreateFolderAsync(string folderName)
    {
        string placeholder = $"{folderName.TrimEnd('/')}/.keep";
        var blobClient = _containerClient.GetBlobClient(placeholder);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(""));
        await blobClient.UploadAsync(stream, overwrite: true);
        Console.WriteLine($"Папку '{folderName}' створено.");
    }

    private async Task<List<string>> GetAllBlobNamesAsync()
    {
        var files = new List<string>();
        await foreach (BlobItem blob in _containerClient.GetBlobsAsync())
        {
            files.Add(blob.Name);
        }
        return files;
    }

    public async Task<List<string>> ListFolderContentsAsync(string folderName)
    {
        string prefix = folderName.TrimEnd('/') + "/";
        var allFiles = await GetAllBlobNamesAsync();
        var items = allFiles.Where(f => f.StartsWith(prefix)).ToList();

        foreach (var item in items)
            Console.WriteLine($"  - {item}");

        Console.WriteLine($"Знайдено {items.Count} файлів у папці '{folderName}'.");
        return items;
    }

    public async Task<List<string>> ListFoldersAsync()
    {
        var allFiles = await GetAllBlobNamesAsync();
        var folders = new HashSet<string>();

        foreach (var file in allFiles)
        {
            int slashIndex = file.IndexOf('/');
            if (slashIndex > 0)
            {
                string folder = file.Substring(0, slashIndex + 1);
                if (folders.Add(folder))
                    Console.WriteLine($"📁 {folder}");
            }
        }

        Console.WriteLine($"Знайдено папок: {folders.Count}");
        return folders.ToList();
    }

    public async Task DeleteFolderAsync(string folderName)
    {
        string prefix = folderName.TrimEnd('/') + "/";
        var allFiles = await GetAllBlobNamesAsync();
        var toDelete = allFiles.Where(f => f.StartsWith(prefix)).ToList();

        foreach (var blobName in toDelete)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        Console.WriteLine($"Папку '{folderName}' видалено ({toDelete.Count} файлів).");
    }

    public async Task<List<string>> ListAllFilesAsync()
    {
        var files = await GetAllBlobNamesAsync();
        Console.WriteLine($"Всього файлів у контейнері: {files.Count}");
        return files;
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        const string connectionString = "YOUR_CONNECTION_STRING";
        const string containerName = "test-container";

        var service = new AzureBlobService(connectionString, containerName);

        Console.WriteLine("=== 1. Створення контейнера ===");
        await service.CreateContainerAsync();

        Console.WriteLine("\n=== 2. Створення папок ===");
        await service.CreateFolderAsync("documents");
        await service.CreateFolderAsync("images");

        Console.WriteLine("\n=== 3. Список папок ===");
        await service.ListFoldersAsync();

        Console.WriteLine("\n=== 4. Завантаження файлів ===");
        await service.UploadTextAsync("documents/hello.txt", "Привіт, Azure Blob Storage!");
        await service.UploadTextAsync("documents/notes.txt", "Нотатки: перший запис.");
        await service.UploadTextAsync("images/photo-info.txt", "Опис фото.");

        Console.WriteLine("\n=== 5. Читання файлу ===");
        string content = await service.ReadFileAsync("documents/hello.txt");
        Console.WriteLine($"Вміст: {content}");

        Console.WriteLine("\n=== 6. Оновлення файлу ===");
        await service.UpdateFileAsync("documents/hello.txt", "Оновлений вміст файлу!");
        string updated = await service.ReadFileAsync("documents/hello.txt");
        Console.WriteLine($"Оновлений вміст: {updated}");

        Console.WriteLine("\n=== 7. Список файлів у папці documents ===");
        await service.ListFolderContentsAsync("documents");

        Console.WriteLine("\n=== 8. Скачати файл на диск ===");
        await service.DownloadFileAsync("documents/notes.txt", "downloaded_notes.txt");

        Console.WriteLine("\n=== 9. Перевірка існування ===");
        bool exists = await service.FileExistsAsync("documents/hello.txt");
        Console.WriteLine($"documents/hello.txt існує: {exists}");

        Console.WriteLine("\n=== 10. Видалення файлу ===");
        await service.DeleteFileAsync("documents/hello.txt");
        bool existsAfter = await service.FileExistsAsync("documents/hello.txt");
        Console.WriteLine($"documents/hello.txt після видалення: {existsAfter}");

        Console.WriteLine("\n=== 11. Видалення папки images ===");
        await service.DeleteFolderAsync("images");

        Console.WriteLine("\n=== 12. Всі файли у контейнері ===");
        var allFiles = await service.ListAllFilesAsync();
        foreach (var f in allFiles)
            Console.WriteLine($"  {f}");

        Console.WriteLine("\n=== 13. Видалення контейнера ===");
        await service.DeleteContainerAsync();

        Console.WriteLine("\n✅ Всі операції виконано успішно!");
    }
}