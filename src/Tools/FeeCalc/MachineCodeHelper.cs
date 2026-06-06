using System;
using System.Collections.Generic;
using System.Management;
using System.Security.Cryptography;
using System.Text;


namespace FMO.FeeCalc;

public static class MachineCodeHelper
{
    /// <summary>
    /// 获取硬件组合机器码（32位MD5大写）
    /// </summary>
    /// <returns>唯一机器码</returns>
    public static string GetMachineCode()
    {
        try
        {
            StringBuilder sb = new StringBuilder();

            //1.CPU序列号
            string cpuSN = GetWmiInfo("Win32_Processor", "ProcessorId");
            sb.Append(cpuSN);

            //2.主板序列号
            string boardSN = GetWmiInfo("Win32_BaseBoard", "SerialNumber");
            sb.Append(boardSN);

            //3.系统盘硬盘序列号
            string diskSN = GetSystemDiskSerial();
            sb.Append(diskSN);

            //4.第一个可用网卡MAC
            string mac = GetFirstMacAddress();
            sb.Append(mac);

            //MD5加密生成机器码
            string raw = sb.ToString().Trim();
            if (string.IsNullOrEmpty(raw))
                return GenerateRandomFallbackCode();

            return GetMD5(raw).ToUpper();
        }
        catch
        {
            //异常兜底：随机+CPU信息生成备用码
            return GenerateRandomFallbackCode();
        }
    }

    #region WMI读取硬件信息
    /// <summary>WMI查询单个字段</summary>
    private static string GetWmiInfo(string className, string fieldName)
    {
        try
        {
            using ManagementClass mc = new ManagementClass(className);
            using ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                var val = mo[fieldName]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(val))
                    return val;
            }
        }
        catch { }
        return "";
    }

    /// <summary>获取系统所在硬盘序列号</summary>
    private static string GetSystemDiskSerial()
    {
        try
        {
            //系统盘一般是盘符0
            using ManagementClass diskClass = new ManagementClass("Win32_LogicalDisk");
            using ManagementObjectCollection disks = diskClass.GetInstances();
            foreach (ManagementObject disk in disks)
            {
                var devId = disk["DeviceID"]?.ToString();
                if (devId == null) continue;
                //取C盘
                if (devId.Equals("C:", StringComparison.OrdinalIgnoreCase))
                {
                    var diskSN = disk["VolumeSerialNumber"]?.ToString();
                    return diskSN ?? "";
                }
            }
        }
        catch { }
        return "";
    }

    /// <summary>获取第一个物理网卡MAC（排除虚拟网卡）</summary>
    private static string GetFirstMacAddress()
    {
        try
        {
            using ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            using ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                bool enabled = mo["IPEnabled"] != null && (bool)mo["IPEnabled"];
                var mac = mo["MacAddress"]?.ToString();
                if (enabled && !string.IsNullOrWhiteSpace(mac))
                {
                    return mac.Replace(":", "").Replace("-", "");
                }
            }
        }
        catch { }
        return "";
    }
    #endregion

    #region MD5加密
    private static string GetMD5(string input)
    {
        using MD5 md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        StringBuilder sb = new StringBuilder();
        foreach (byte b in hashBytes)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
    #endregion

    #region 读取硬件异常兜底随机码
    private static string GenerateRandomFallbackCode()
    {
        //读取系统唯一标识+随机，防止取不到硬件信息
        string info = Environment.MachineName + Environment.OSVersion + Guid.NewGuid().ToString("N").Substring(0, 12);
        return GetMD5(info).ToUpper();
    }
    #endregion
}