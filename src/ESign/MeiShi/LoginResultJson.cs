namespace FMO.ESigning.MeiShi;


internal class LoginResultJson
{
    /// <summary>
    /// 
    /// </summary>
    public bool success { get; set; }
    /// <summary>
    /// 账号不存在或密码错误
    /// </summary>
    public string? message { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int code { get; set; }
 
    /// <summary>
    /// 
    /// </summary>
    //public int timestamp { get; set; } 

}