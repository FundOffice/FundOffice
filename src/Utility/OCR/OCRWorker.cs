using FMO.Logging;
using OpenCvSharp;
using RapidOCRSharpOnnx;
using RapidOCRSharpOnnx.Configurations;
using RapidOCRSharpOnnx.Providers;
using RapidOCRSharpOnnx.Utils;

namespace FMO.OCR;

public static class OCRWorker
{
    public static async Task<string> VerifyCode(byte[] buf)
    {
        try
        {
            string modelBasePath = @"modelfiles\"; // 你的模型文件所在目录

            // Mobile 版本模型
            string detectPath = Path.Combine(modelBasePath, "ch_PP-OCRv5_det_mobile.onnx");
            string recPath = Path.Combine(modelBasePath, "ch_PP-OCRv5_rec_mobile.onnx");
            string clsPath = Path.Combine(modelBasePath, "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx");


            using RapidOCRSharp ocr = new RapidOCRSharp(new ExecutionProviderCPU(new OcrConfig(detectPath, recPath, LangRec.CH, OCRVersion.PPOCRV5, clsPath)));




            using (Mat src = Cv2.ImDecode(buf, ImreadModes.Grayscale))
                return string.Join(' ', ocr.RecognizeText(src).TextBlocks);

        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return "";
        }
    }
}
