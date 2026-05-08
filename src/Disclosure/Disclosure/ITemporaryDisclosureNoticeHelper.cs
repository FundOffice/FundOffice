using FMO.Logging;
using FMO.Models;
using FMO.TPL;
using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Disclosure;

public static class ITemporaryDisclosureNoticeHelper
{
    public static void MakeWord(this ITemporaryDisclosureNotice notice, string tplName, object obj)
    {
        // 按模板生成
        try
        {
            var outp = Path.GetFullPath(@$"temp\{DateTime.Now.Ticks}.docx");
            if (Tpl.GenerateByPredefined(outp, tplName, obj))
            {
                if (notice.Word?.File is not null)
                    notice.Word.File.Delete();

                notice.Word = new Models.SimpleFile(FileMeta.Create(outp, tplName));
            }
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }
    }
}
