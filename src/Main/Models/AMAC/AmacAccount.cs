namespace FMO.Models;

public record class AmacAccount(string Id, string Name, string Password, bool IsValid);

public record class AmacDirectAccount(string Name, string Password, string Secret);

public record class PfidDirectAccount(string Id, string Name, string Password, string Key, bool IsValid) : AmacAccount(Id, Name, Password, IsValid);

public record class AmacReportAccount(string Id, string Name, string Password, string Key, bool IsValid) : AmacAccount(Id, Name, Password, IsValid);