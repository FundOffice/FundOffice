using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using MoT;
using System;
using System.Collections.Generic;
using System.Text;

namespace Utilities;



public class Toast
{

    /// <summary>
    /// 普通信息提示
    /// </summary>
    /// <param name="message">提示内容</param>
    public static void Info(string message) =>
        WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Information, message));

    /// <summary>
    /// 成功提示
    /// </summary>
    /// <param name="message">提示内容</param>
    public static void Success(string message) =>
        WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Success, message));

    /// <summary>
    /// 警告提示
    /// </summary>
    /// <param name="message">提示内容</param>
    public static void Warning(string message) =>
        WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, message));

    /// <summary>
    /// 错误提示
    /// </summary>
    /// <param name="message">提示内容</param>
    public static void Error(string message) =>
        WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Error, message));
}
