using FMO.Models;
using ICSharpCode.SharpZipLib.Zip;

namespace FMO.Utilities;

public class ZipSplitter
{
    public static string[] CreateSplitZip(string[] filePaths, string outputFolder, string baseFileName, long splitSizeBytes)
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        var volumePaths = new List<string>();

        // 核心修复：确保外层流生命周期覆盖内层流
        using (var splitStream = new SplitZipStream(outputFolder, baseFileName, splitSizeBytes, volumePaths))
        {
            using (var zipStream = new ZipOutputStream(splitStream))
            {
                zipStream.SetLevel(9);
                zipStream.UseZip64 = UseZip64.Off;

                foreach (var filePath in filePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    var entry = new ZipEntry(Path.GetFileName(filePath)) { DateTime = DateTime.Now };
                    zipStream.PutNextEntry(entry);

                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    fs.CopyTo(zipStream);
                    zipStream.CloseEntry();
                }

                // 必须在此处 Finish，确保所有数据写入 splitStream
                zipStream.Finish();
            } // zipStream 在此处 Dispose，此时 splitStream 仍存活，允许 Flush
        } // splitStream 在此处 Dispose

        return volumePaths.ToArray();
    }

    public static string[] CreateSplitZip(FileMeta[] files, string outputDir, string baseFileName, long splitSizeBytes)
    {
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var volumePaths = new List<string>();

        using (var splitStream = new SplitZipStream(outputDir, baseFileName, splitSizeBytes, volumePaths))
        {
            using (var zipStream = new ZipOutputStream(splitStream))
            {
                zipStream.SetLevel(9);
                zipStream.UseZip64 = UseZip64.Off;

                foreach (var file in files)
                {
                    if (file is null || !file.Exists) continue;

                    var entry = new ZipEntry(file.Name) { DateTime = DateTime.Now };
                    zipStream.PutNextEntry(entry);

                    using var fs = file.OpenRead();
                    if (fs != null) fs.CopyTo(zipStream);
                    zipStream.CloseEntry();
                }

                zipStream.Finish();
            }
        }

        return volumePaths.ToArray();
    }
}

/// <summary>
/// 修复版 SplitZipStream：增加状态检查，彻底解决 ObjectDisposedException
/// </summary>
public class SplitZipStream : Stream
{
    private readonly string _outputFolder;
    private readonly string _baseFileName;
    private readonly long _maxVolumeSize;
    private readonly List<string> _volumeList;

    private FileStream? _currentStream;
    private long _currentSize;
    private int _volumeIndex = 1;
    private bool _isDisposed = false;

    public SplitZipStream(string outputFolder, string baseFileName, long maxVolumeSize, List<string> volumeList)
    {
        _outputFolder = outputFolder ?? throw new ArgumentNullException(nameof(outputFolder));
        _baseFileName = baseFileName ?? throw new ArgumentNullException(nameof(baseFileName));
        _maxVolumeSize = maxVolumeSize;
        _volumeList = volumeList ?? throw new ArgumentNullException(nameof(volumeList));

        CreateNewVolume();
    }

    private void CreateNewVolume()
    {
        // 关闭上一个流（如果存在）
        if (_currentStream != null)
        {
            _currentStream.Flush();
            _currentStream.Dispose();
            _currentStream = null;
        }

        string fileName = _volumeIndex == 1
            ? $"{_baseFileName}.zip"
            : $"{_baseFileName}.{_volumeIndex:D2}.zip";

        string fullPath = Path.Combine(_outputFolder, fileName);

        // 创建新流：使用 WriteThrough 确保实时写入
        _currentStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        _currentSize = 0;

        _volumeList.Add(fullPath);
        _volumeIndex++;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckIfDisposed();

        int remaining = count;
        while (remaining > 0)
        {
            if (_currentStream == null || _currentSize >= _maxVolumeSize)
            {
                CreateNewVolume();
            }

            long canWrite = _maxVolumeSize - _currentSize;
            int writeLength = (int)Math.Min(remaining, canWrite);

            // 修复：正确的偏移计算
            _currentStream.Write(buffer, offset + (count - remaining), writeLength);

            _currentSize += writeLength;
            remaining -= writeLength;
        }
    }

    public override void Flush()
    {
        CheckIfDisposed();
        _currentStream?.Flush();
    }

    private void CheckIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SplitZipStream), "流已被释放，无法操作");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 仅在托管资源释放时，执行一次清理
            if (!_isDisposed)
            {
                _currentStream?.Flush();
                _currentStream?.Dispose();
                _currentStream = null;
                _isDisposed = true;
            }
        }
        base.Dispose(disposing);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();
    public override void SetLength(long value)
        => throw new NotSupportedException();
}