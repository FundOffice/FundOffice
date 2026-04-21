using LiteDB;

namespace FMO.Schedule;

public class MissionDatabase : LiteDatabase
{

    public MissionDatabase() : base(@"FileName=data\mission.db;Password=891uiu89f41uf9dij432u89;Connection=Shared")
    {
    }



}
