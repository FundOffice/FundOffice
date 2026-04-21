using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMO.Utilities;

public class NumberHelper
{
    public static string NumberToChinese(decimal number)
    {
        string[] numArray = { "零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖" };
        string[] unitArray = { "", "拾", "佰", "仟" };
        string[] sectionArray = { "", "万", "亿", "万亿" };

        string numStr = Math.Abs(number).ToString("0.00");
        string[] parts = numStr.Split('.');
        string integerPart = parts[0];
        string decimalPart = parts[1];

        string result = "";
        int sectionIndex = 0;
        while (integerPart.Length > 0)
        {
            string section = integerPart.Length >= 4 ? integerPart.Substring(integerPart.Length - 4) : integerPart;
            integerPart = integerPart.Length >= 4 ? integerPart.Substring(0, integerPart.Length - 4) : "";

            string sectionResult = "";
            bool zeroFlag = false;
            for (int i = 0; i < section.Length; i++)
            {
                int num = int.Parse(section[i].ToString());
                if (num == 0)
                {
                    zeroFlag = true;
                }
                else
                {
                    if (zeroFlag)
                    {
                        sectionResult = "零" + sectionResult;
                        zeroFlag = false;
                    }
                    sectionResult = sectionResult + numArray[num] + unitArray[section.Length - i - 1] ;
                }
            }

            if (sectionResult != "")
            {
                sectionResult += sectionArray[sectionIndex];
            }
            result = sectionResult + result;
            sectionIndex++;
        }

        if (result == "")
        {
            result = "零";
        }

        if (decimalPart != "00")
        {
            result += "元";
            int jiao = int.Parse(decimalPart[0].ToString());
            int fen = int.Parse(decimalPart[1].ToString());
            if (jiao != 0)
            {
                result += numArray[jiao] + "角";
            }
            else if (fen != 0)
            {
                result += "零";
            }
            if (fen != 0)
            {
                result += numArray[fen] + "分";
            }
        }
        else
        {
            result += "元整";
        }

        if (number < 0)
        {
            result = "负" + result;
        }

        return result;
    }
}