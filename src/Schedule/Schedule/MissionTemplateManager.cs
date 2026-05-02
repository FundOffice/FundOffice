using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FMO.Schedule;


 

//public static class MissionTemplateManager
//{
//    [ModuleInitializer]
//    public static void RegisterPredefined()
//    {
//        MissionManager.Register(typeof(MailCacheMission), () => new MailCacheMission(), m => new MailCacheViewModel((MailCacheMission)m));
//        MissionManager.Register(typeof(DailyFromMailMission), () => new DailyFromMailMission(), m => new DailyFromMailViewModel((DailyFromMailMission)m));
//        MissionManager.Register(typeof(TAFromMailMission), () => new TAFromMailMission(), m => new TAFromMailViewModel((TAFromMailMission)m));
//        MissionManager.Register(typeof(DisclosureFromMailMission), () => new DisclosureFromMailMission(), m => new DisclosureFromMailViewModel((DisclosureFromMailMission)m));
//        MissionManager.Register(typeof(SendDailyReportToWebhookMission), () => new SendDailyReportToWebhookMission(), m => new SendDailyReportToWebhookViewModel((SendDailyReportToWebhookMission)m));

//    }

//}
