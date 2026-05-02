namespace SmartDocumentProcessingSystem.Configuration;

public class ProcessingOptions
{
    public string SampleDataPath { get; set; } = "../sample-data/resources";
    public string TesseractCommand { get; set; } = "tesseract";
}
