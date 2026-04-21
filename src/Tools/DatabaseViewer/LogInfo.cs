using FMO.Models;
using LiteDB;
using Serilog;
using Serilog.Events;

namespace DatabaseViewer
{
    public class LogInfo
    {
        [LiteDB.BsonField("_t")]
        public DateTime Time { get; set; }


        [BsonField("_l")]
        public LogEventLevel Level { get; set; } 

        [BsonField("_m")]
        public string? Message { get; set; }
    }
}