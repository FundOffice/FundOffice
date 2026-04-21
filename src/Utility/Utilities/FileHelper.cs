using System.Security.Cryptography;

namespace FMO.Utilities;

public static class FileHelper
{
    /// <summary>
    /// 计算文件hash
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? ComputeHash(string path)
    {
        if (!File.Exists(path)) return null;

        using (var md5 = MD5.Create())
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) 
            {
                var hashc = md5.ComputeHash(stream);
                return BitConverter.ToString(hashc).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public static string? ComputeHash(Stream? stream)
    {
        if (stream is null) return null;

        using (var md5 = MD5.Create())
        {
            if(stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
                var hashc = md5.ComputeHash(stream);
                return BitConverter.ToString(hashc).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fi"></param>
    /// <returns></returns>
    public static string? ComputeHash(this FileInfo fi)
    {
        return ComputeHash(fi.FullName);
    }


    /// <summary>
    /// 应对复制文件时，重名问题，生成可用文件名
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static (string Name, string FullName) GenerateUniqueFileName(string directory, string fileName)
    {
        // 分离文件名和扩展名
        string fileBaseName = Path.GetFileNameWithoutExtension(fileName);
        string fileExtension = Path.GetExtension(fileName);
        string fullPath = Path.Combine(directory, fileName);

        // 检查文件是否存在
        if (!File.Exists(fullPath))
            return (fileName, fullPath);

        // 生成新的文件名，添加递增的数字后缀
        int counter = 2;
        while (true)
        {
            string newFileName = $"{fileBaseName} ({counter}){fileExtension}";
            string newFullPath = Path.Combine(directory, newFileName);
            if (!File.Exists(newFullPath))
                return (newFileName, newFullPath);
            counter++;
        }
    }


    public static (string Path, bool Copied) CopyFile(FileInfo ori, string tarFolder)
    {
        var tar = Path.Combine(tarFolder, ori.Name);

        // 不存在重名文件，直接复制
        if (!File.Exists(tar))
        {
            File.Copy(ori.FullName, tar);
            return (tar, true);
        }

        var hash = ori.ComputeHash();
        var oldhash = ComputeHash(tar);

        // 同名，但是文件一样，直接返回
        if (hash == oldhash)
            return (tar, false);

        var fileName = ori.Name;
        string fileBaseName = Path.GetFileNameWithoutExtension(fileName);
        string fileExtension = Path.GetExtension(fileName);

        // 生成新的文件名，添加递增的数字后缀
        int counter = 1;
        while (++counter > 0)
        {
            string newFileName = $"{fileBaseName} ({counter}){fileExtension}";
            string newFullPath = Path.Combine(tarFolder, newFileName);
            if (File.Exists(newFullPath))
            {
                var h = ComputeHash(newFullPath);
                if (hash == oldhash)
                    return (newFullPath, false);

                continue;
            }

            File.Copy(ori.FullName, newFullPath);
            return (newFullPath, true);
        }

        return (string.Empty, false);
    }



    public static string? CopyFile2(FileInfo ori, string tarFolder, string? name = null)
    {
        var fileName = string.IsNullOrWhiteSpace(name) ? ori.Name : name; 

        var tar = Path.Combine(tarFolder, fileName);

        // 如果源和目的一致
        if (ori.FullName == Path.GetFullPath(tar))
            return ori.FullName;


        // 不存在重名文件，直接复制
        if (!File.Exists(tar))
        {
            File.Copy(ori.FullName, tar);
            return tar;
        }

        string fileBaseName = Path.GetFileNameWithoutExtension(fileName);
        string fileExtension = Path.GetExtension(fileName);

        // 生成新的文件名，添加递增的数字后缀
        //int counter = 1;
        //while (++counter > 0  )
        for (int counter = 1; counter < 999; counter++)
        {
            string newFileName = $"{fileBaseName} ({counter}){fileExtension}";
            string newFullPath = Path.Combine(tarFolder, newFileName);
            if (File.Exists(newFullPath))
                continue;

            File.Copy(ori.FullName, newFullPath);
            return newFullPath;
        }

        return null;
    }

}
