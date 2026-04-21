using Serilog;
using System.Runtime.CompilerServices;

namespace FMO.Logging;



public static class LogEx
{
    public static ILogger Here(
         [CallerFilePath] string filePath = "",
         [CallerMemberName] string memberName = "",
         [CallerLineNumber] int lineNumber = 0)
    {
        var className = Path.GetFileNameWithoutExtension(filePath);
        return Log.Logger
            .ForContext("File", className)
            .ForContext("Method", memberName)
            .ForContext("Line", lineNumber);
    }

    // 包装常用的日志方法
    public static void Verbose(string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Verbose(message);
    }

    public static void Verbose<T>(string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Verbose(message, propertyValue);
    }

    public static void Verbose<T0, T1>(string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Verbose(message, propertyValue0, propertyValue1);
    }

    public static void Verbose<T0, T1, T2>(string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Verbose(message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Verbose(string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Verbose(message, propertyValues);
    }

    public static void Debug(string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Debug(message);
    }

    public static void Debug<T>(string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Debug(message, propertyValue);
    }

    public static void Debug<T0, T1>(string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Debug(message, propertyValue0, propertyValue1);
    }

    public static void Debug<T0, T1, T2>(string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Debug(message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Debug(string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Debug(message, propertyValues);
    }

    public static void Information(string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Information(message);
    }

    public static void Information<T>(string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Information(message, propertyValue);
    }

    public static void Information<T0, T1>(string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Information(message, propertyValue0, propertyValue1);
    }

    public static void Information<T0, T1, T2>(string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Information(message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Information(string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Information(message, propertyValues);
    }

    public static void Warning(string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Warning(message);
    }

    public static void Warning<T>(string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Warning(message, propertyValue);
    }

    public static void Warning<T0, T1>(string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Warning(message, propertyValue0, propertyValue1);
    }

    public static void Warning<T0, T1, T2>(string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Warning(message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Warning(string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Warning(message, propertyValues);
    }

    public static void Error(string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(message);
    }

    public static void Error<T>(string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(message, propertyValue);
    }

    public static void Error<T0, T1>(string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(message, propertyValue0, propertyValue1);
    }

    public static void Error<T0, T1, T2>(string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Error(string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(message, propertyValues);
    }

    public static void Error(Exception exception,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error($"{exception.Message}\n{exception.StackTrace}");
    }

    public static void Error(string msg, Exception exception,
      [CallerFilePath] string filePath = "",
      [CallerMemberName] string memberName = "",
      [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error($"msg\n{exception.Message}\n{exception.StackTrace}");
    }

    public static void Error<T>(Exception exception, string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(exception, message, propertyValue);
    }

    public static void Error<T0, T1>(Exception exception, string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(exception, message, propertyValue0, propertyValue1);
    }

    public static void Error<T0, T1, T2>(Exception exception, string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(exception, message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Error(Exception exception, string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Error(exception, message, propertyValues);
    }

    public static void Fatal(string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(message);
    }

    public static void Fatal<T>(string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(message, propertyValue);
    }

    public static void Fatal<T0, T1>(string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(message, propertyValue0, propertyValue1);
    }

    public static void Fatal<T0, T1, T2>(string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Fatal(string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(message, propertyValues);
    }

    public static void Fatal(Exception exception, string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(exception, message);
    }

    public static void Fatal<T>(Exception exception, string message, T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(exception, message, propertyValue);
    }

    public static void Fatal<T0, T1>(Exception exception, string message, T0 propertyValue0, T1 propertyValue1,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(exception, message, propertyValue0, propertyValue1);
    }

    public static void Fatal<T0, T1, T2>(Exception exception, string message, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(exception, message, propertyValue0, propertyValue1, propertyValue2);
    }

    public static void Fatal(Exception exception, string message, object[] propertyValues,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Here(filePath, memberName, lineNumber).Fatal(exception, message, propertyValues);
    }
}