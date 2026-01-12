#!/usr/bin/env dotnet-script
#r "EmailCorrespondenceSplitter.Console/bin/Debug/net8.0/EmailCorrespondenceSplitter.Console.dll"
#r "EmailCorrespondenceSplitter.Console/bin/Debug/net8.0/MsgReader.dll"

using EmailCorrespondenceSplitter.Services;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var emailParser = new MsgEmailParser();
var correspondenceDetector = new CorrespondenceDetector();
var testEmailPath = "EmailCorrespondenceSplitter.Tests/Assets/em6.msg";

var email = await emailParser.ParseAsync(testEmailPath);
var correspondences = correspondenceDetector.DetectCorrespondences(email);

Console.WriteLine($"Total correspondences: {correspondences.Count}");
for (int i = 0; i < correspondences.Count; i++)
{
    var c = correspondences[i];
    Console.WriteLine($"\nCorrespondence {i}:");
    Console.WriteLine($"  From: {c.From}");
    Console.WriteLine($"  SentOn: {c.SentOn}");
    Console.WriteLine($"  IsParent: {c.IsParent}");
    Console.WriteLine($"  HtmlContent length: {c.HtmlContent?.Length ?? 0}");
}
