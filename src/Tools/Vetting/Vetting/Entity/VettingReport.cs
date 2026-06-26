using LiteDB;

namespace Vetting.Entity;
public record VettingReport([property: BsonId] string Id, string Name, DateTime CreateTime);
