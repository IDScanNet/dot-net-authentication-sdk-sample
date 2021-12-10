using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IDScanNet.Validation;
using IDScanNet.Validation.SDK;
using Newtonsoft.Json;

namespace TestApp
{
    public class Program
    {
        private static ValidationService _validationService;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

            Stopwatch sw = Stopwatch.StartNew();
            //Init ValidationService
            Console.WriteLine("Init started");
            await Init();
            sw.Stop();
            Console.WriteLine("Init time " + sw.ElapsedMilliseconds);

            //Validate suspicious document
            await Validate("Suspicious");
            //Validate face
            await Validate("Face");
            //Validate DL front
            await Validate("DLFront");
            //Validate DL back
            await Validate("DLBack");
            //Validate passport front
            await Validate("PassportFront");
            //Validate spoofing fake
            await Validate("BNWAntiSpoofing");
            //Validate Rfid
            await Validate("Rfid");


            Console.ReadLine();
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            _validationService.Dispose();
        }


        private static async Task Init()
        {
            Directory.CreateDirectory("Validation Logs");
            //Simple validation settings
            var validationServiceSettings = new ValidationServiceSettings
            {
                //Set logging directory(default is c:\Users\Public\Documents\IDScan.net\Validation.SDK\Logs\)
                LoggingDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Validation Logs"),
                HostDataDirectoryPath = @"C:\ProgramData\IDScan.net\IDScanNet.Validation.SDK.Data",

                //Advanced validation settings:
                //Host - pipe name for connection. If it's not stated, then the default one will be used
                //Port - ignore this for now, as it would be required sometime in the future
               // HostDirectoryPath = @"c:\Projects\IDScanNet.Validation.SDK.Host\IDScanNet.Validation.SDK.Host\bin\Debug\net5.0-windows7\"
                //HostDataDirectoryPath - folder with data-files; can be substituted with a different value in case it's part of an app and data-files are somewhere close
            };

            _validationService = new ValidationService(validationServiceSettings);

            //Fired when the document processing stage is changed
            _validationService.ProcessingStageChanged += (sender, s) =>
            {
                Console.WriteLine(s.Status);
            };
            //Fired when the error has occurred
            _validationService.ErrorReceived += (sender, s) =>
            {
                Console.WriteLine("Error:" + s.Text);
            };

            //Asynchronously initialize validation service
            await _validationService.InitializeAsync();
        }

        private static async Task Validate(String folder)
        {
            Console.WriteLine("----------------------------------------------------------------------------------------");
            Console.WriteLine($"Validate {folder}:");
            Stopwatch sw = Stopwatch.StartNew();

            //Create a new validation request
            var request = new ValidationRequest();
            request.Id = Guid.NewGuid();
            request.Scan = new ScanResult();
            var documentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);

            //Set RawString with a RawDataSource type
            var file = Directory.GetFiles(documentPath, "Pdf417RawData.txt").FirstOrDefault();
            if (file != null)
                request.Scan.RawItems = new Dictionary<RawDataSource, RawData>
            {
                {
                    RawDataSource.PDF417, new RawData
                    {
                        RawString = await File.ReadAllTextAsync(file)
                    }
                }
            };
            //Set Images by ImageType
            request.Scan.ScannedImages = new Dictionary<ImageType, byte[]>();

            file = Directory.GetFiles(documentPath, "Normal.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.ColorFront, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "NormalBack.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.ColorBack, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "UV.*").FirstOrDefault();
            if (file != null)
                 request.Scan.ScannedImages.Add(ImageType.UVFront, await File.ReadAllBytesAsync(file));
            
            file = Directory.GetFiles(documentPath, "UVBack.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.UVBack, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "IR.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.IRFront, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "IRBack.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.IRBack, await File.ReadAllBytesAsync(file));

            file = Directory.GetFiles(documentPath, "Face.*").FirstOrDefault();
            if (file != null)
                request.Scan.ScannedImages.Add(ImageType.CameraFace, await File.ReadAllBytesAsync(file));

            
            //Asynchronously validate document
            ValidationResponse result = await _validationService.ProcessAsync(request);

            Console.WriteLine();
          
            if (result.Result != null)
            {
                sw.Stop();
                Console.WriteLine($"Validation Result for {folder} ElapsedMilliseconds: {sw.ElapsedMilliseconds}:");
                String group = "";
                foreach (var testResult in result.Result.Results.OrderBy(x=>x.TestGroup))
                {
                    if (group != testResult.TestGroup.ToString())
                    {
                        group = testResult.TestGroup.ToString();
                        Console.WriteLine($"{group}:");
                    }
                    Console.WriteLine($"    {testResult.Name} - {testResult.Type} {testResult.TestStatus} {testResult.Confidence}");
                    if (testResult.CrossMatches != null)
                        foreach (var match in testResult.CrossMatches)
                        {
                            Console.WriteLine(
                                $"          {match.FieldName} - {match.Item1.DataSource} = {match.Item1.Value};  {match.Item2.DataSource} = {match.Item2.Value} Confidence = {testResult.Confidence}");
                        }
                }
                Console.WriteLine();
                //Final validation result
                Console.WriteLine("Validation status = " + result.Result?.ValidationStatus);

                Console.WriteLine();
                Console.WriteLine("Document property value: ");
                Console.WriteLine();
                var document = JsonConvert.SerializeObject(result.Document, Formatting.Indented);
                Console.WriteLine(document);
                Console.WriteLine();
                Console.WriteLine("PlainDocument property value: ");
                Console.WriteLine();
                var plainDocument = JsonConvert.SerializeObject(result.PlainDocument, Formatting.Indented);
                Console.WriteLine(plainDocument);
            }
           
            Console.WriteLine();
        }
       

    }
}