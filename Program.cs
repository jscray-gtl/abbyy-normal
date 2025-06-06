﻿using Jint;    
using System;    
using System.IO;    
using System.Threading.Tasks;    
using System.Net.Http;    
using System.Net.Http.Headers;    
using System.Text;    
using System.Collections.Generic;    
using Newtonsoft.Json;  
using Newtonsoft.Json.Linq;  
  
public enum ContentEncoding  
{  
    UTF8,  
    ASCII,  
    Unicode  
}  
  
public class HttpRequest  
{  
    private readonly HttpClient _httpClient;  
    private HttpContent? _httpContent;  
    private MultipartFormDataContent? _multipartFormDataContent;  
    private string? _url;  
    private string? _method;  
    private string? _authToken;  
    private string? _authScheme;  
    private bool _throwExceptionOnFailed = false;  
    private TimeSpan _timeout = TimeSpan.FromMinutes(1);  
  
    public string? Url  
    {  
        get { return _url; }  
        set { _url = value; }  
    }  
  
    public string? Method  
    {  
        get { return _method; }  
        set { _method = value; }  
    }  
  
    public string? AuthToken  
    {  
        get { return _authToken; }  
        set { _authToken = value; }  
    }  
  
    public string? AuthScheme  
    {  
        get { return _authScheme; }  
        set { _authScheme = value; }  
    }  
  
    public bool ThrowExceptionOnFailed  
    {  
        get { return _throwExceptionOnFailed; }  
        set { _throwExceptionOnFailed = value; }  
    }  
  
    public double Timeout  
    {  
        get { return _timeout.TotalMinutes; }  
        set { _timeout = TimeSpan.FromMinutes(value); }  
    }  
  
    public string? ResponseText { get; private set; }  
    public string? ResponseType { get; private set; }  
    public int Status { get; private set; }  
  
    public HttpRequest()  
    {  
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Default");
        _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("application/x-www-form-urlencoded");
    }  
  
    public void SetHeader(string name, string value)  
    {  
        if (_httpContent != null)  
        {  
            _httpContent.Headers.TryAddWithoutValidation(name, value);  
        }  
        else  
        {  
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(name, value);  
        }  
    }  
  
    public void Send()
    {
        SendAsync().GetAwaiter().GetResult();
    }
    public async Task SendAsync()  
    {  
        if (!string.IsNullOrEmpty(_authToken))  
        {  
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_authScheme ?? "Bearer", _authToken);  
        }  
  
        _httpClient.Timeout = _timeout;  
  
        if (_url == null)  
        {  
            throw new InvalidOperationException("Url is not set");  
        }  
  
        if (_method == null)  
        {  
            throw new InvalidOperationException("Method is not set");  
        }  
  
        HttpResponseMessage? response = null;  
        switch (_method.ToUpper())  
        {  
            case "POST":  
                if (_httpContent == null)  
                {  
                    throw new InvalidOperationException("Content is not set");  
                }  
                response = await _httpClient.PostAsync(_url, _httpContent);  
                break;  
            case "PUT":  
                if (_httpContent == null)  
                {  
                    throw new InvalidOperationException("Content is not set");  
                }  
                response = await _httpClient.PutAsync(_url, _httpContent);  
                break;  
            case "GET":  
                response = await _httpClient.GetAsync(_url);  
                break;  
            default:  
                throw new NotSupportedException("Method not supported");  
        }  
  
        if (response != null)  
        {  
            Status = (int)response.StatusCode;  
            ResponseText = await response.Content.ReadAsStringAsync();  
            ResponseType = response.Content.Headers.ContentType?.ToString();  
  
            if (_throwExceptionOnFailed && !response.IsSuccessStatusCode)  
            {  
                Console.WriteLine($"Request Failed!!!! to {_url}");
                Console.WriteLine("Request Failed!!!!");

                throw new HttpRequestException($"Request failed to {_url}, with status code {Status}");  
            }  
        }  
    }  
  
    public void SetFileContent(byte[] fileData, string fileName, string? mediaType = null)  
    {  
        var content = new ByteArrayContent(fileData);  
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType ?? GetMimeType(fileName));  
        _httpContent = content;  
        _method = "POST";  
    }  
  
    public void SetStringContent(string content, ContentEncoding? contentEncoding = null, string? mediaType = null)  
    {  
        var encoding = contentEncoding switch  
        {  
            ContentEncoding.UTF8 => Encoding.UTF8,  
            ContentEncoding.ASCII => Encoding.ASCII,  
            ContentEncoding.Unicode => Encoding.Unicode,  
            _ => Encoding.UTF8  
        };  
  
        var stringContent = new StringContent(content, encoding, mediaType ?? "application/json");  
        _httpContent = stringContent;  
        _method = "POST";  
    }  
  

    public void SetUrlFormEncodedContent(object content)  
    {  
        var keyValuePairs = new List<KeyValuePair<string, string>>();  
        var jObject = JObject.FromObject(content);  
        foreach (var property in jObject.Properties())  
        {  
            if (property.Value != null)  
            {  
                keyValuePairs.Add(new KeyValuePair<string, string>(property.Name, property.Value.ToString()));  
            }  
        }  
    
        var formUrlEncodedContent = new FormUrlEncodedContent(keyValuePairs);  
        _httpContent = formUrlEncodedContent;  
        _method = "POST";  
    }  
  
    public void AppendStringContent(string data, string contentName)  
    {  
        if (_multipartFormDataContent == null)  
        {  
            _multipartFormDataContent = new MultipartFormDataContent();  
            _httpContent = _multipartFormDataContent;  
            _method = "POST";  
        }  
  
        _multipartFormDataContent.Add(new StringContent(data), contentName);  
    }  
  
    public void AppendFileContent(byte[] fileData, string contentName, string fileName)  
    {  
        if (_multipartFormDataContent == null)  
        {  
            _multipartFormDataContent = new MultipartFormDataContent();  
            _httpContent = _multipartFormDataContent;  
            _method = "POST";  
        }  
  
        var fileContent = new ByteArrayContent(fileData);  
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));  
        _multipartFormDataContent.Add(fileContent, contentName, fileName);  
    }  
  
    private string GetMimeType(string fileName)  
    {  
        var extension = System.IO.Path.GetExtension(fileName).ToLower();  
        switch (extension)  
        {  
            case ".jpg":  
            case ".jpeg":  
                return "image/jpeg";  
            case ".png":  
                return "image/png";  
            default:  
                return "application/octet-stream";  
        }  
    }  
}  
  
public class MultipartFormDataRequest  
{  
    private readonly HttpRequest _request;  
  
    public MultipartFormDataRequest()  
    {  
        _request = new HttpRequest();
        _request.SetHeader("Content-Type", "application/x-www-form-urlencoded");
    }  
  
    public string? Url  
    {  
        get { return _request.Url; }  
        set { _request.Url = value; }  
    }  
  
    public string? Method  
    {  
        get { return _request.Method; }  
        set { _request.Method = value; }  
    }  
  
    public string? AuthToken  
    {  
        get { return _request.AuthToken; }  
        set { _request.AuthToken = value; }  
    }  
  
    public string? AuthScheme  
    {  
        get { return _request.AuthScheme; }  
        set { _request.AuthScheme = value; }  
    }  
  
    public void AppendStringContent(string data, string contentName)  
    {  
        _request.AppendStringContent(data, contentName);  
    }  
  
    public void AppendFileContent(byte[] fileData, string contentName, string fileName)  
    {  
        _request.AppendFileContent(fileData, contentName, fileName);  
    }  
  
    public async Task SendAsync()  
    {  
        await _request.SendAsync();  
    }  
  
    public string? ResponseText  
    {  
        get { return _request.ResponseText; }  
    }  
}  
    
public class Transaction    
{    
    public Document[] Documents { get; set; }    
}    
    
public class Document    
{    
    public ExportResult[] Exports { get; set; }    
}    
    
public class ExportResult    
{    
    public string ExportFormat { get; set; }    
    public string ToJson()    
    {    
        // Return stubbed JSON data  
        return @"{""key"":""value""}";      
    }    
    public byte[] FileData { get; set; }    
}    
    
public class ExportFormat    
{    
    public static string Json = "Json";    
    public static string Pdf = "Pdf";    
}  

public class Context    
{    
    public Transaction Transaction { get; set; }    
    // public ExportFormat ExportFormat { get; set; }    
    public MultipartFormDataRequest CreateMultipartFormDataRequest()    
    {    
        return new MultipartFormDataRequest();    
    }    
    
    public HttpRequest CreateHttpRequest()    
    {    
        return new HttpRequest();    
    }
    public Context()  
    {  
        Transaction = new Transaction  
        {  
            Documents = new[]  
            {  
                new Document  
                {  
                    Exports = new[]  
                    {  
                        new ExportResult  
                        {  
                            ExportFormat = ExportFormat.Json  
                        },  
                        new ExportResult  
                        {  
                            ExportFormat = ExportFormat.Pdf,  
                            FileData = new byte[] { 1, 2, 3 }  
                        }  
                    }  
                }
            }  
        };  
    }  
}    
  
class Program    
{    
    static async Task Main(string[] args)    
    {    
        var engine = new Engine();    
        engine.SetValue("console", new    
        {    
            log = new Action<object>(Console.WriteLine),    
            error = new Action<object>(Console.Error.WriteLine),    
            warn = new Action<object>(Console.WriteLine)    
        });    
    
        var context = new Context();  
        var exportFormat = new ExportFormat();
        if (context != null)    
        {    
            engine.SetValue("Context", context);    
            engine.SetValue("ExportFormat", exportFormat);    
        }    
    
        if (File.Exists("test.js"))    
        {    
            try    
            {    
                var script = await File.ReadAllTextAsync("test.js");    
                engine.Execute(script);
            }    
            catch (Jint.Runtime.JavaScriptException ex)    
            {    
                Console.WriteLine($"JavaScript error: {ex.Message}");    
            }    
        }    
        else    
        {    
            Console.WriteLine("test.js not found");    
        }    
    }    
}  