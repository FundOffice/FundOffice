namespace Vetting.Entity;



public enum ChangedType { Add, Update, Delete }

public record AIProviderChanged(int Id, ChangedType Type);