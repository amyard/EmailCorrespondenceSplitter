#!/usr/bin/env dotnet-script
#r "nuget: MsgReader, 5.6.1"
#r "nuget: HtmlAgilityPack, 1.11.71"

using System.Text;
using MsgReader.Outlook;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

// Register encoding provider
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var emailPath = "EmailCorrespondenceSplitter.Tests/bin/Debug/net8.0/Assets/em5.msg";

// Parse the email
using var msg = new Storage.Message(emailPath);
var htmlBody = msg.BodyHtml ?? "";

Console.WriteLine("=== ORIGINAL EMAIL HTML BODY ===");
Console.WriteLine(htmlBody);
Console.WriteLine("\n=== END ORIGINAL HTML ===\n");

// Load HTML document
var doc = new HtmlDocument();
doc.LoadHtml(htmlBody);

// Find blockquotes
var allBlockquotes = doc.DocumentNode.SelectNodes("//blockquote[@type='cite']");

Console.WriteLine($"\n=== FOUND {allBlockquotes?.Count ?? 0} BLOCKQUOTES ===\n");

if (allBlockquotes != null)
{
    for (int i = 0; i < allBlockquotes.Count; i++)
    {
        Console.WriteLine($"\n--- BLOCKQUOTE {i + 1} ---");
        Console.WriteLine(allBlockquotes[i].OuterHtml.Substring(0, Math.Min(500, allBlockquotes[i].OuterHtml.Length)));
        Console.WriteLine("...");
        
        // Check if nested
        var parent = allBlockquotes[i].ParentNode;
        bool isNested = false;
        while (parent != null)
        {
            if (parent.Name == "blockquote" && parent.GetAttributeValue("type", "") == "cite")
            {
                isNested = true;
                break;
            }
            parent = parent.ParentNode;
        }
        Console.WriteLine($"Is Nested: {isNested}");
    }
}

Console.WriteLine("\n=== PROCESSING COMPLETE ===");
