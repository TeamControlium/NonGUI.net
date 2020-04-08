﻿// <copyright file="HTTPBased.cs" company="TeamControlium Contributors">
//     Copyright (c) Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
namespace TeamControlium.NonGUI
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Security;
    using System.Runtime.Remoting;
    using System.Security.Cryptography.X509Certificates;
    using TeamControlium.Utilities;
    using static TeamControlium.Utilities.Log;
    using static TeamControlium.Utilities.Repository;

    /// <summary>
    /// Provides test oriented functionality for interacting with HTTP based protocols
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <listheader>
    /// <term>Category</term><term>Item</term><term>Type</term><term>Default Value</term><term>Comments</term>
    /// </listheader>
    /// <item>
    /// <term>TeamControlium.HTTP</term><term>SSLPort</term><term><code>int</code></term><term>443</term><term>Port used for SSL tunnelled HTTP (HTTPS) communication</term>
    /// </item>
    /// <item>
    /// <term>TeamControlium.HTTP</term><term>HTTPPort</term><term><code>int</code></term><term>80</term><term>Port used for unsecure HTTP communication</term>
    /// </item>
    /// </list>
    /// </remarks>
    public class HTTPBased
    {
        /// <summary>
        /// If transaction logging required, contains full path &amp; filename for logging of HTTP/TCP transactions.
        /// </summary>
        private string transactionsLogFile = GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "TCP_TransactionsLogFile", null);

        /// <summary>
        /// TCP object representing TCB layer of connection for TCP based interactions.
        /// </summary>
        private TCPBased tcpRequest;

        /// <summary>
        /// Initialises a new instance of the <see cref="HTTPBased" /> class. Used for testing an HTTP interface when used for Non-UI interaction (IE. WebServices, <code>JSON</code> etc...)
        /// </summary>
        public HTTPBased()
        {
            if (!string.IsNullOrEmpty(this.transactionsLogFile))
            {
                General.WriteTextToFile(this.transactionsLogFile, General.WriteMode.Append, $"HTTPNonUI Instantiated at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
                LogWriteLine(LogLevels.FrameworkInformation, $"Writing HTTPNonUI transactions to LogFile > {this.transactionsLogFile}");
            }

            this.tcpRequest = new TCPBased();
            this.tcpRequest.ClientCertificate = this.ClientCertificate;
            this.tcpRequest.CertificateValidationCallback = this.CertificateValidationCallback;
        }

        /// <summary>
        /// Gets or sets X509 Certificate to use with SSL based communications (If required)
        /// </summary>
        public X509Certificate2 ClientCertificate { get; set; } = null;

        /// <summary>
        /// Gets or sets Call-back delegate for custom/test-based validations of Server certificates.  Can be used for Server Certificate logging, negative testing etc...
        /// </summary>
        public RemoteCertificateValidationCallback CertificateValidationCallback { get; set; } = null;

        /// <summary>
        /// Gets raw Response text of HTTP Request.  Data is raw HTTP response.
        /// </summary>
        public string ResponseRaw { get; private set; }

        /// <summary>
        /// TCP Port used for SSL (https://) based communications.  Is usually port 443.
        /// </summary>
        private int SSLPort => GetItemLocalOrDefault<int>("TeamControlium.HTTP", "SSLPort", 443);

        /// <summary>
        /// TCP Port used for unsecure HTTP (http://) based communications.  Is usually port 80.
        /// </summary>
        private int HTTPPort => GetItemLocalOrDefault<int>("TeamControlium.HTTP", "HTTPPort", 80);

        /// <summary>
        /// Performs an HTTP POST to the required Domain/ResourcePath containing given HTTP Header and Body
        /// </summary>
        /// <param name="domain">Domain to POST to.  IE. <code>www.mytestsite.com</code></param>
        /// <param name="resourcePath">Resource path at domain.  IE. <code>respource/path</code></param>
        /// <param name="header">HTTP Header items, not including top line (IE. HTTP Method, resource, version</param>
        /// <param name="body">HTTP Body</param>
        /// <returns>Processed HTTP Response</returns>
        /// <remarks>
        /// Content-Length header item is automatically added (or, if exists in given header, modified) during building of Request.
        /// If Connection keep-alive is used, currently request will time-out on waiting for response.  Intelligent and async functionality needs building in.
        /// Aspects of request (such as port, header/request layout etc.) can be modified using settings stored in Repository.  See <see cref="HTTPBased"/>
        /// and documentation for details of Repository items referenced.
        /// <list type="table">
        /// Processed HTTP Response is passed back as a collection of Name/Value pairs.  The following raw HTTP response is converted;
        /// <code>
        /// HTTP/1.1 200 OK<br/>
        /// Cache-Control: private, max-age=0<br/>
        /// Content-Length: 346<br/>
        /// Content-Type: text/xml; charset=utf-8<br/>
        /// Server: Server<br/>
        /// Web-Service: DataFlex 18.1<br/>
        /// Access-Control-Allow-Origin: http://www.dataaccess.com<br/>
        /// Access-Control-Allow-Methods: GET, POST<br/>
        /// Access-Control-Allow-Headers: content-type<br/>
        /// Access-Control-Allow-Credentials: true<br/>
        /// Strict-Transport-Security: max-age=31536000<br/>
        /// Date: Tue, 07 Apr 2020 22:16:05 GMT<br/>
        /// Connection: close<br/>
        /// <br/>
        /// &lt;?xml version="1.0" encoding="utf-8"?&gt;<br/>
        /// &lt;soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"&gt;<br/>
        ///   &lt;soap:Body&gt;<br/>
        ///     &lt;m:NumberToWordsResponse xmlns:m="http://www.dataaccess.com/webservicesserver/"&gt;<br/>
        ///       &lt;m:NumberToWordsResult&gt;seventy eight &lt;/m:NumberToWordsResult&gt;<br/>
        ///     &lt;/m:NumberToWordsResponse&gt;<br/>
        ///   &lt;/soap:Body&gt;<br/>
        /// &lt;/soap:Envelope&gt;
        /// </code>
        /// Most items are self-explanatory;
        /// <listheader>
        /// <term>Item Name</term><term>Example</term><term>Comments</term>
        /// </listheader>
        /// <item>
        /// <term>HTTPVersion</term><term>1.1</term><term>HTTP Version - Always in Processed response</term>
        /// </item>
        /// <item>
        /// <term>StatusCode</term><term>200</term><term>Status code - Always in Processed response</term>
        /// </item>
        /// <item>
        /// <term>StatusText</term><term>OK</term><term>Status text - Always in Processed response</term>
        /// </item>
        /// <item>
        /// <term>Cache-Control</term><term>private; max-age=0</term><term>Dependant on server and application</term>
        /// </item>
        /// <item>
        /// <term>Content-Length</term><term>346</term><term>Number of characters in Body - Always in Processed response</term>
        /// </item>
        /// <item>
        /// <term>Content-Type</term><term>text/xml; charset=utf-8</term><term>Type of body data - Always in Processed response</term>
        /// </item>
        /// <item>
        /// <term>Server</term><term>Server</term><term>Dependant on server and application</term>
        /// </item>
        /// <item>
        /// <term>Web-Service</term><term>DataFlex 18.1</term><term>Dependant on server and application</term>
        /// </item>
        /// <item>
        /// <term>Access-Control-Allow-Origin</term><term>http</term><term></term>
        /// </item>
        /// <item>
        /// <term>Access-Control-Allow-Methods</term><term>GET</term><term>POST</term><term></term>
        /// </item>
        /// <item>
        /// <term>Access-Control-Allow-Headers</term><term>content-type</term><term></term>
        /// </item>
        /// <item>
        /// <term>Access-Control-Allow-Credentials</term><term>true</term><term></term>
        /// </item>
        /// <item>
        /// <term>Strict-Transport-Security</term><term>max-age=31536000</term><term></term>
        /// </item>
        /// <item>
        /// <term>Date</term><term>Tue, 07 Apr 2020 22</term><term>Server date</term>
        /// </item>
        /// <item>
        /// <term>Connection</term><term>close</term><term>Indicates what state the Server will hold connection at end of response - Always in Processed response</term>
        /// </item>
        /// <item>
        /// <term>Body</term><term>&lt;http&gt;&lt;body&gt;hello&lt;/body&gt;&lt;/http&gt;</term><term>Raw body of HTTP Response</term>
        /// </item>
        /// </list>
        /// </remarks>
        public Dictionary<string, string> HttpPOST(string domain, string resourcePath, string header, string body)
        {
            FullHTTPRequest httpPayload = new FullHTTPRequest(FullHTTPRequest.HTTPMethods.Post, resourcePath, (string)null, header, body);
            return this.DecodeResponse(this.tcpRequest.DoTCPRequest(null, null, domain, this.HTTPPort, httpPayload.ToString(true)));
        }

        /// <summary>
        /// Decode HTTP Response into easily read Name/Value pair details within Dictionary type
        /// </summary>
        /// <param name="rawData">Raw HTTP Response string</param>
        /// <returns>Processed HTTP Response</returns>
        private Dictionary<string, string> DecodeResponse(string rawData)
        {
            Dictionary<string, string> returnData = new Dictionary<string, string>();
            this.ResponseRaw = rawData;

            try
            {
                // Do First line (IE. HTTP/1.1 200 OK)
                if (string.IsNullOrEmpty(rawData) || string.IsNullOrWhiteSpace(rawData))
                {
                    returnData.Add("HTTPVersion", "Unknown - Empty Response");
                    returnData.Add("StatusCode", "Unknown - Empty Response");
                    return returnData;
                }

                // We have something.....  Is it HTTP?
                if (!rawData.StartsWith("HTTP"))
                {
                    string firstLine = rawData.Split('\r')[0];
                    firstLine = (firstLine.Length >= 20) ? firstLine.Substring(0, 17) + "..." : firstLine;
                    returnData.Add("HTTPVersion", string.Format("Unknown - Response not HTTP: FirstLine = [{0}]", firstLine));
                    returnData.Add("StatusCode", "Unknown - Response not HTTP");
                    return returnData;
                }

                // Get the header out first....
                string headerArea = rawData.Substring(0, rawData.IndexOf("\r\n\r\n"));

                // And the HTML body
                string bodyArea = rawData.Substring(rawData.IndexOf("\r\n\r\n") + 4, rawData.Length - rawData.IndexOf("\r\n\r\n") - 4);

                // Split & check first line
                string[] firstLineSplit = headerArea.Split('\r')[0].Split(' ');
                if (firstLineSplit.Length < 3 || !firstLineSplit[0].Contains('/'))
                {
                    string firstLine = headerArea.Split('\r')[0];
                    firstLine = (firstLine.Length >= 20) ? firstLine.Substring(0, 17) + "..." : firstLine;
                    returnData.Add("HTTPVersion", string.Format("Unknown - Response header top line not in correct format: [{0}]", firstLine));
                    returnData.Add("StatusCode", "Unknown - Response not formatted correctly");
                    return returnData;
                }

                // Finally, we can process the first line....
                returnData.Add("HTTPVersion", firstLineSplit[0].Split('/')[1]);
                returnData.Add("StatusCode", firstLineSplit[1]);
                string statusText = null;
                for (int index = 2; index < firstLineSplit.Length; index++)
                {
                    statusText = statusText + " " + firstLineSplit[index];
                }

                statusText = statusText.Trim();
                returnData.Add("StatusText", statusText);

                // And do the rest of the header...  We do a for loop as we want to ignore the top line; it is just the HTTP protocol and version info
                string[] headerSplit = headerArea.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                for (int index = 1; index < headerSplit.Length; index++)
                {
                    if (!headerSplit[index].Contains(':'))
                    {
                        throw new InvalidHTTPResponse("Response contained invalid header line [{0}]. No colon (:) present: [{1}]", index.ToString(), headerSplit[index]);
                    }
                    else
                    {
                        returnData.Add(headerSplit[index].Split(':')[0].Trim(), headerSplit[index].Split(':')[1].Trim());
                    }
                }

                // And finally the body...
                // First, we need to know if the body is chunked. It if is we need to de-chunk it....
                if (returnData.ContainsKey("Transfer-Encoding") && returnData["Transfer-Encoding"] == "chunked")
                {
                    // So, we need to dechunk the data.....
                    // Data is chunked as follows
                    // <Number of characters in hexaecimal>\r\n
                    // <Characters in chunk>\r\n
                    // this repeats until;
                    // 0\r\n
                    // \r\n
                    bool dechunkingFinished = false;
                    string workingBody = string.Empty;
                    string chunkHex;
                    int chunkLength;
                    while (!dechunkingFinished)
                    {
                        // Itterates through the chunked body area
                        // Get the Chunk HEX
                        chunkHex = bodyArea.Substring(0, bodyArea.IndexOf("\r\n"));
                        bodyArea = bodyArea.Substring(chunkHex.Length + 2, bodyArea.Length - (chunkHex.Length + 2));

                        if (!int.TryParse(chunkHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out chunkLength))
                        {
                            throw new InvalidHTTPResponse("[HTTP]DecodeResponse: Fatal error decoding chunked html body. Parsing Hex [{0}] failed)", chunkHex);
                        }

                        if (chunkLength == 0)
                        {
                            break;
                        }

                        workingBody += bodyArea.Substring(0, chunkLength);
                        bodyArea = bodyArea.Substring(chunkLength, bodyArea.Length - chunkLength);

                        if (!bodyArea.StartsWith("\r\n"))
                        {
                            InvalidHTTPResponse ex = new InvalidHTTPResponse("[HTTP]DecodeResponse: Fatal error decoding chunked html body. End of chunk length not CRLF!)", chunkLength);
                            ex.Data.Add("Chunk Length", chunkLength);
                            ex.Data.Add("Chunk Data", bodyArea);
                            throw ex;
                        }

                        bodyArea = bodyArea.Substring(2, bodyArea.Length - 2);
                    }

                    returnData.Add("Body", workingBody);
                }
                else
                {
                    // No chunked so just grab the body
                    returnData.Add("Body", bodyArea);
                    return returnData;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidHTTPResponse("[HTTP]DecodeResponse: Fatal error decoding raw response string header)", ex);
            }

            return returnData;
        }

        /// <summary>
        /// Stores and formats the Header and/or Body of an HTTP request
        /// </summary>
        /// <remarks>
        /// Uses Local <see cref="TeamControlium.Utilities.Repository"/> data items to configure how the HTTP request is built.
        /// <list type="table">
        /// <listheader>
        /// <term>Category</term><term>Item</term><term>Type</term><term>Default Value</term><term>Comments</term>
        /// </listheader>
        /// <item>
        /// <term>TeamControlium.HTTPNonUI</term><term>HTTPHeader_ItemDelimiter</term><term>string</term><term>:</term><term>Character(s) between Header title and value</term>
        /// </item>
        /// <item>
        /// <term>TeamControlium.HTTPNonUI</term><term>HTTPHeader_SpaceAfterItemDelimiter</term><term>bool</term><term>true</term><term>Defines whether a space should follow the Header Item delimiter</term>
        /// </item>
        /// <item>
        /// <term>TeamControlium.HTTPNonUI</term><term>HTTPHeader_ItemsLineTerminator</term><term>string</term><term>\r\n</term><term>Character(s) at end of each header item (IE. Item delimiter in header)</term>
        /// </item>
        /// <item>
        /// <term>TeamControlium.HTTPNonUI</term><term>HeaderItemText_ContentLength</term><term>string</term><term>Content-Length</term><term>Title of Header item determining content length.  Should always be Content-Length but tests can change this for negative testing if required.</term>
        /// </item>
        /// <item>
        /// <term>TeamControlium.HTTPNonUI</term><term>HeaderBodyDelimiter</term><term>string</term><term>\r\n</term><term>Character(s) delimiting between HTTP Header and Body. Should always be CRLF (Specification states a CRLF without preceding characters) but tests can change this for negative testing if required.</term>
        /// </item>
        /// </list>
        /// </remarks>
        private class FullHTTPRequest
        {
            /// <summary>
            /// We store the header as a string as this is Test oriented.  Although the Header of an HTTP document is Name: Value pairs the test may have loaded
            /// an invalid string for negative testing.  In which case we need to preserve that.  If we converted to a dictionary it may not work or it may remove the
            /// customisation the test has added.
            /// </summary>
            private string header;

            /// <summary>
            /// We store the query as a string as this is Test oriented.  Although the query of an HTTP URL is Name=Value pairs the test may have loaded
            /// an invalid string for negative testing.  In which case we need to preserve that.  If we converted to a dictionary it may not work or it may remove the
            /// customisation the test has added.
            /// </summary>
            private string query;

            /// <summary>
            /// Stores the required HTTP Method.  May be null in which case it is ignored.  This may be the case when the user has explicitly put the HPP Method in the request string.
            /// </summary>
            private HTTPMethods? httpMethod;
            
            /// <summary>
            /// Resource Path string
            /// </summary>
            private string resourcePath;

            /// <summary>
            /// Initialises a new instance of the <see cref="FullHTTPRequest" /> class.  Creates HTTP Request data containing given header and body.
            /// </summary>
            /// <param name="header">Header details of HTTP Request.</param>
            /// <param name="body">Body part of HTTP request to be sent</param>
            /// <remarks>
            /// If header is null, repository data item [TeamControlium.HTTPNonUI,HTTPHeader] is checked for and used if set.  See <see cref="HeaderDefault"/>
            /// </remarks>
            public FullHTTPRequest(string? header, string? body)
            {
                this.httpMethod = null;
                this.resourcePath = string.Empty;
                this.SetHeader(header);
                this.Body = (body == null) ? string.Empty : body;
            }

            /// <summary>
            /// Initialises a new instance of the <see cref="FullHTTPRequest" /> class.  Creates HTTP Request data containing given header and body.
            /// </summary>
            /// <param name="httpMethod">HTTP Method to use in Header</param>
            /// <param name="resourcePath">Resource Path of this HTTP call</param>
            /// <param name="queryParameters">Query Parameters to be appended to Resource Path (Note delimiter is NOT needed)</param>
            /// <param name="header">Header details of HTTP Request.</param>
            /// <param name="body">Body part of HTTP request to be sent</param>
            /// <remarks>
            /// If header is null, repository data item [TeamControlium.HTTPNonUI,HTTPHeader] is checked for and used if set.  See <see cref="HeaderDefault"/>.
            /// It is the caller's responsibility to ensure header top line is NOT part of the header string as this will result in it being used twice!
            /// </remarks>
            public FullHTTPRequest(HTTPMethods httpMethod, string resourcePath, string queryParameters, string? header, string? body)
            {
                this.httpMethod = httpMethod;
                this.resourcePath = resourcePath ?? string.Empty;
                this.SetHeader(header);
                this.SetQueryParameters(queryParameters);
                this.Body = (body == null) ? string.Empty : body;
            }

            /// <summary>
            /// Initialises a new instance of the <see cref="FullHTTPRequest" /> class.  Creates HTTP Request data containing given header and body.
            /// </summary>
            /// <param name="httpMethod">HTTP Method to use in Header</param>
            /// <param name="resourcePath">Resource Path of this HTTP call</param>
            /// <param name="queryParameters">Query Parameters to be appended to Resource Path (Note delimiter is NOT needed)</param>
            /// <param name="header">Header items to be used in request. Dictionary is unwrapped and converted to string.</param>
            /// <param name="body">Body part of HTTP request to be sent</param>
            /// <remarks>
            /// If header is null, repository data item [TeamControlium.HTTPNonUI,HTTPHeader] is checked for and used if set.  See <see cref="HeaderDefault"/>.
            /// It is the caller's responsibility to ensure header top line is NOT part of the header string as this will result in it being used twice!
            /// Note.  Aspects of the query string (delimiters etc) can be modified using Repository data items.  See <see cref="FullHTTPRequest"/> documentation for details.
            /// </remarks>
            public FullHTTPRequest(HTTPMethods httpMethod, string resourcePath, string queryParameters, Dictionary<string, string>? header, string? body)
            {
                this.httpMethod = httpMethod;
                this.resourcePath = resourcePath ?? string.Empty;
                this.SetHeader(header);
                this.SetQueryParameters(queryParameters);
                this.Body = (body == null) ? string.Empty : body;
            }

            /// <summary>
            /// Initialises a new instance of the <see cref="FullHTTPRequest" /> class.  Creates HTTP Request data containing given header and body.
            /// </summary>
            /// <param name="httpMethod">HTTP Method to use in Header</param>
            /// <param name="resourcePath">Resource Path of this HTTP call</param>
            /// <param name="queryParameters">Query Parameters to be appended to Resource Path. Dictionary is unwrapped and converted to string.</param>
            /// <param name="header">Header details of HTTP Request.</param>
            /// <param name="body">Body part of HTTP request to be sent</param>
            /// <remarks>
            /// If header is null, repository data item [TeamControlium.HTTPNonUI,HTTPHeader] is checked for and used if set.  See <see cref="HeaderDefault"/>.
            /// It is the caller's responsibility to ensure header top line is NOT part of the header string as this will result in it being used twice!
            /// Note.  Aspects of the query string (delimiters etc) can be modified using Repository data items.  See <see cref="FullHTTPRequest"/> documentation for details.
            /// </remarks>
            public FullHTTPRequest(HTTPMethods httpMethod, string resourcePath, Dictionary<string, string>? queryParameters, string header, string body)
            {
                this.httpMethod = httpMethod;
                this.resourcePath = resourcePath ?? string.Empty;
                this.SetHeader(header);
                this.SetQueryParameters(queryParameters);
                this.Body = (body == null) ? string.Empty : body;
            }

            /// <summary>
            /// Initialises a new instance of the <see cref="FullHTTPRequest" /> class.  Creates HTTP Request data containing given header and body.
            /// </summary>
            /// <param name="httpMethod">HTTP Method to use in Header</param>
            /// <param name="resourcePath">Resource Path of this HTTP call</param>
            /// <param name="queryParameters">Query Parameters to be appended to Resource Path. Dictionary is unwrapped and converted to string.</param>
            /// <param name="header">Header items to be used in request. Dictionary is unwrapped and converted to string.</param>
            /// <param name="body">Body part of HTTP request to be sent</param>
            /// <remarks>
            /// If header is null, repository data item [TeamControlium.HTTPNonUI,HTTPHeader] is checked for and used if set.  See <see cref="HeaderDefault"/>.
            /// It is the caller's responsibility to ensure header top line is NOT part of the header string as this will result in it being used twice!
            /// Note.  Aspects of the query string (delimiters etc) can be modified using Repository data items.  See <see cref="FullHTTPRequest"/> documentation for details.
            /// </remarks>
            public FullHTTPRequest(HTTPMethods httpMethod, string resourcePath, Dictionary<string, string>? queryParameters, Dictionary<string, string>? header, string? body)
            {
                this.httpMethod = httpMethod;
                this.resourcePath = resourcePath ?? string.Empty;
                this.SetHeader(header);
                this.SetQueryParameters(queryParameters);
                this.Body = (body == null) ? string.Empty : body;
            }

            /// <summary>
            /// Possible HTTP Methods
            /// </summary>
            public enum HTTPMethods
            {
                /// <summary>
                /// HTTP GET Method defined when building Request Header.
                /// The POST method is used to submit an entity to the specified resource, often causing a change in state or side effects on the server.
                /// </summary>
                Post,

                /// <summary>
                /// HTTP POST Method defined when building Request Header.
                /// The GET method requests a representation of the specified resource. Requests using GET should only retrieve data
                /// </summary>
                Get,

                /// <summary>
                /// HTTP HEAD Method defined when building Request Header.
                /// The HEAD method asks for a response identical to that of a GET request, but without the response body.
                /// </summary>
                Head,

                /// <summary>
                /// HTTP PUT Method defined when building Request Header.
                /// The PUT method replaces all current representations of the target resource with the request payload.
                /// </summary>
                Put,

                /// <summary>
                /// HTTP DELETE Method defined when building Request Header.
                /// The DELETE method deletes the specified resource.
                /// </summary>
                Delete,

                /// <summary>
                /// HTTP CONNECT Method defined when building Request Header.
                /// The CONNECT method establishes a tunnel to the server identified by the target resource.
                /// </summary>
                Connect,

                /// <summary>
                /// HTTP OPTIONS Method defined when building Request Header.
                /// The OPTIONS method is used to describe the communication options for the target resource.
                /// </summary>
                Options,

                /// <summary>
                /// HTTP TRACE Method defined when building Request Header.
                /// The TRACE method performs a message loop-back test along the path to the target resource.
                /// </summary>
                Trace,

                /// <summary>
                /// HTTP PATCH Method defined when building Request Header.
                /// The PATCH method is used to apply partial modifications to a resource.
                /// </summary>
                Patch
            }

            /// <summary>
            /// Gets or sets HTTP Request body
            /// </summary>
            public string Body
            {
                get;
                set;
            }

            /// <summary>
            /// Separator between URL Resource Path and Query string.  Should be ?
            /// </summary>
            private string HttpURLQuerySeparator => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPURL_QuerySeparator", "?");

            /// <summary>
            /// Separator between query items in URL.  Should be #
            /// </summary>
            private string HttpURLQueryParameterSeparator => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPURL_ParameterSeparator", "#");

            /// <summary>
            /// Separator between name and value of each query parameter.  Should be =
            /// </summary>
            private string HttpURLQueryParameterNameValueSeparator => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPURL_ParameterNameValueSeparator", "=");

            /// <summary>
            /// First Text in an HTTP request to denote an HTTP POST Method.  Should be POST
            /// </summary>
            private string HttpTypePostText => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPHeader_PostText", "POST");

            /// <summary>
            /// First Text in an HTTP request to denote an HTTP GET Method.  Should be GET
            /// </summary>
            private string HttpTypeGetText => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPHeader_PostText", "GET");

            /// <summary>
            /// Text in HTTP request top line to indicate HTTP version document is compliant with.  Should be HTTP/1.1
            /// </summary>
            private string HttpVersion => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPHeader_Version", "HTTP/1.1");

            /// <summary>
            /// Name/Value delimiter for HPP Request header items.  Should be :
            /// </summary>
            private string HeaderItemDelimiter => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPHeader_ItemDelimiter", ":");

            /// <summary>
            /// Flag indicates if a space character should follow <see cref="HeaderItemDelimiter"/>.  Should be true
            /// </summary>
            private bool SpaceAfterHeaderItemDelimiter => GetItemLocalOrDefault<bool>("TeamControlium.HTTPNonUI", "HTTPHeader_SpaceAfterItemDelimiter", true);

            /// <summary>
            /// HTTP Request header line termination characters.  Should be \r\n
            /// </summary>
            private string HeaderItemLineTerminator => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HTTPHeader_ItemsLineTerminator", "\r\n");

            /// <summary>
            /// Text to use for HTTP Header Content Length item.  Should be Content-Length
            /// </summary>
            private string HeaderContentLengthTitle => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HeaderItemText_ContentLength", "Content-Length");

            /// <summary>
            /// Delimiter between HTTP Request header items and the body.  Specification states this must be a CRLF with no preceding characters  
            /// </summary>
            private string HeaderBodyDelimiter => GetItemLocalOrDefault<string>("TeamControlium.HTTPNonUI", "HeaderBodyDelimiter", "\r\n");

            /// <summary>
            /// Gets Header from Repository data. If null Header is passed in to a call (or a public Web method with no option to set a Header is used), this property is used.  Property is populated from
            /// the local Repository item [TeamControlium.HTTPNonUI,HTTPHeader] which can be a string or Dictionary&lt;string, string&gt;.  If this has not been set an empty
            /// header is used.  If Repository item [TeamControlium.HTTPNonUI,HTTPHeader] contains a type NOT string or Dictionary&lt;string, string&gt; an exception is thrown.
            /// </summary>
            private dynamic HeaderDefault
            {
                get
                {
                    dynamic head = GetItemLocalOrDefault("TeamControlium.HTTPNonUI", "HTTPHeader", new Dictionary<string, string>());
                    Type headerType = ((ObjectHandle)head).Unwrap().GetType();
                    if (headerType == typeof(string))
                    {
                        return (string)head;
                    }
                    else if (headerType == typeof(Dictionary<string, string>))
                    {
                        return (Dictionary<string, string>)head;
                    }
                    else
                    {
                        throw new Exception("Local repository [TeamControlium.HTTPNonUI,HTTPHeader] not stored as Dictionary<string,string> or string.  Cannot use!");
                    }
                }
            }

            /// <summary>
            /// Gets Query string from Repository. If null Query Parameters is passed in to a call (or a public Web method with no option to set a Header is used), this property is used.  Property is populated from
            /// the local Repository item [TeamControlium.HTTPNonUI,HTTPQuery] which can be a string or Dictionary&lt;string, string&lt;.  If this has not been set an empty
            /// header is used.  If Repository item [TeamControlium.HTTPNonUI,HTTPQuery] contains a type NOT string or Dictionary&lt;string, string&gt; an exception is thrown. If populated,
            /// the query part of the URL Resource Path is populated IRRELEVANT of the HTTP Method used - this is to ensure testing (and negative testing ) is possible.  It is the
            /// responsibility of the test code to ensure correct state of the Query string.
            /// </summary>
            private dynamic QueryParametersDefault
            {
                get
                {
                    dynamic param = GetItemLocalOrDefault("TeamControlium.HTTPNonUI", "HTTPQuery", new Dictionary<string, string>());
                    Type paramType = ((ObjectHandle)param).Unwrap().GetType();
                    if (paramType == typeof(string))
                    {
                        return (string)param;
                    }
                    else if (paramType == typeof(Dictionary<string, string>))
                    {
                        return (Dictionary<string, string>)param;
                    }
                    else
                    {
                        throw new Exception("Local repository [TeamControlium.HTTPNonUI,HTTPQuery] not stored as Dictionary<string,string> or string.  Cannot use!");
                    }
                }
            }

            /// <summary>
            /// Processes passed header Dictionary and sets <see cref="header"/> field.
            /// </summary>
            /// <param name="header">Collection of HTTP Request header items</param>
            /// <remarks>
            /// If null, <see cref="HeaderDefault"/> used to obtain Repository defined header if set.  If not set, Header is set to an empty string.
            /// </remarks>
            public void SetHeader(Dictionary<string, string>? header)
            {
                if (header == null)
                {
                    if (this.HeaderDefault.GetType() == typeof(string))
                    {
                        this.header = (string)this.HeaderDefault;
                    }
                    else
                    {
                        this.SetHeader((Dictionary<string, string>)this.HeaderDefault);
                    }
                }
                else
                {
                    this.header = string.Join(this.HeaderItemLineTerminator, header.Select(eachHeader => $"{eachHeader.Key}{this.HeaderItemDelimiter}{(this.SpaceAfterHeaderItemDelimiter ? " " : "")}{eachHeader.Value}"));
                }
            }

            /// <summary>
            /// Sets <see cref="header"/> field to passed header if not null
            /// </summary>
            /// <param name="header">Header string to use</param>
            /// <remarks>
            /// If null, <see cref="HeaderDefault"/> used to obtain Repository defined header if set.  If not set, Header is set to an empty string.
            /// </remarks>
            public void SetHeader(string? header)
            {
                if (header == null)
                {
                    if (this.HeaderDefault.GetType() == typeof(string))
                    {
                        this.header = (string)this.HeaderDefault;
                    }
                    else
                    {
                        this.SetHeader((Dictionary<string, string>)this.HeaderDefault);
                    }
                }
                else
                {
                    this.header = header;
                }
            }

            /// <summary>
            /// Processes passed query parameters Dictionary and sets <see cref="query"/> field.
            /// </summary>
            /// <param name="queryParameters">Collection of HTTP Resource query items.</param>
            /// <remarks>
            /// If null, <see cref="QueryParametersDefault"/> used to obtain Repository defined query string/items if set.  If not set, query field is set to an empty string.
            /// </remarks>
            public void SetQueryParameters(Dictionary<string, string>? queryParameters)
            {
                if (queryParameters == null)
                {
                    if (this.QueryParametersDefault.GetType() == typeof(string))
                    {
                        this.query = (string)this.QueryParametersDefault;
                    }
                    else
                    {
                        this.SetQueryParameters((Dictionary<string, string>)this.QueryParametersDefault);
                    }
                }
                else
                {
                    this.query = string.Join(this.HttpURLQueryParameterSeparator, queryParameters.Select(eachParameter => $"{eachParameter.Key}{this.HttpURLQueryParameterNameValueSeparator}{eachParameter.Value}"));
                }
            }

            /// <summary>
            /// Sets <see cref="query"/> field to passed query if not null
            /// </summary>
            /// <param name="query">Query string to use (does not require Resource Path Delimiter.  This is added automatically)</param>
            /// <remarks>
            /// If null, <see cref="QueryParametersDefault"/> used to obtain Repository defined query if set.  If not set, query is set to an empty string.
            /// </remarks>
            public void SetQueryParameters(string? query)
            {
                if (query == null)
                {
                    if (this.QueryParametersDefault.GetType() == typeof(string))
                    {
                        this.query = (string)this.QueryParametersDefault;
                    }
                    else
                    {
                        this.SetQueryParameters((Dictionary<string, string>)this.QueryParametersDefault);
                    }
                }
                else
                {
                    this.query = query;
                }
            }

            /// <summary>
            /// Returns current HTTP request header as a dictionary of Name, Value pairs
            /// </summary>
            /// <returns>Current HTTP request header</returns>
            /// <remarks>
            /// <see cref="HeaderItemLineTerminator"/> and <see cref="HeaderItemDelimiter"/> are used for name/value and line delimiting.  If Request string is not using
            /// these an invalid dictionary will be returned or a possible Exception thrown.
            /// </remarks>
            public Dictionary<string, string> GetHeaderAsDictionary()
            {
                return this.header.Split(this.HeaderItemLineTerminator).Select(eachHeader => eachHeader.Split(this.HeaderItemDelimiter)).ToDictionary(eachHeader => eachHeader[0], eachHeader => eachHeader[1].Trim());
            }

            /// <summary>
            /// Returns current HTTP request header string.
            /// </summary>
            /// <returns>Current header (including Top Line) as would be using is HTTP Request </returns>
            public string GetHeaderAsString()
            {
                string topLine = this.BuildTopLine();
                return ((topLine == null) ? string.Empty : topLine + this.HeaderItemLineTerminator) + this.header;
            }

            /// <summary>
            /// Returns full HTTP Request header without automatically adding or updating Content Length header item
            /// </summary>
            /// <returns>Full HTTP Request header without Content Length added/updated (May already have it)</returns>
            /// <remarks>
            /// HTTP Request header may already contain a Content Length item.  When not automatically added/updated <see cref="FullHTTPRequest"/>
            /// does no checking or adding/updating of Content Length header item.
            /// </remarks>
            public new string ToString()
            {
                return this.ToString(false);
            }

            /// <summary>
            /// Returns full HTTP Request header with or without automatically adding or updating Content Length header item
            /// </summary>
            /// <param name="withContentLengthAddedOrUpdated">If true, Content Length item is added or updated with length of <see cref="Body"/></param>
            /// <returns>Full HTTP Request header with or without Content Length added/updated.</returns>
            /// <remarks>
            /// HTTP Request header may already contain a Content Length item.  When not automatically added/updated <see cref="FullHTTPRequest"/>
            /// does no checking or adding/updating of Content Length header item.  If Content Length is to be added or updated <see cref="FullHTTPRequest"/>
            /// counts number of characters in <see cref="Body"/> and checks <see cref="header"/>.  If no text matching <see cref="HeaderContentLengthTitle"/>
            /// exists in header, it is added using name/value delimiter <see cref="HeaderItemDelimiter"/> and terminated using <see cref="HeaderItemLineTerminator"/>.
            /// If <see cref="HeaderContentLengthTitle"/> exists, the associated value is changed to actual Body length.  Note that when updating, a best-attempt
            /// model is used; if header string is (perhaps deliberately) corrupt update may not work correctly.
            /// </remarks>
            public string ToString(bool withContentLengthAddedOrUpdated)
            {
                // Build the top line first - Starts with the HTTP Method is we have it/  If we dont, forget the top line and hope it is in the header string
                string topLine = this.BuildTopLine();

                if (withContentLengthAddedOrUpdated)
                {
                    if (this.header.Contains(this.HeaderContentLengthTitle))
                    {
                        // If we already have a Content-Length header, replace the value.
                        // We dont want to convert it to a Dictionary, replace value then convert back to a string as the header may contain delibertately invalid
                        // items/layout.  So, we dont it the nasty way;
                        // 1. Find headerContentLengthText
                        // 2. Find the location of the first digit (or line terminator) following it.
                        // 3. Find location of character after last digit after first digit (or, again, line terminator)
                        // 4. Build the new Header from all characters leading to item (2) + our length digits + all characters (inclusive) from item (3)
                        // So, first get the index of the the Content Length title first character.
                        int indexOfTitleStart = this.header.IndexOf(this.HeaderContentLengthTitle);

                        // Get the index of the last character preceding a digit or the line terminator starting from the Content Length title first character index
                        int start = this.header.IndexOfAny(("0123456789" + this.HeaderItemLineTerminator).ToCharArray(), indexOfTitleStart) - 1;
                        if (start == -1)
                        {
                            // We could not find a digit/s or line terminator.  So get the index of the last character of the Content Length title.
                            start = indexOfTitleStart + this.HeaderContentLengthTitle.Length - 1;
                            if (this.header.Length - 1 > start && this.header.Substring(start).Contains(this.HeaderItemDelimiter))
                            {
                                // If a Header Item delimiter follows the Content Length title get the index of its last character.
                                start += this.HeaderItemDelimiter.Length;
                            }
                        }

                        // Start now points to character before first digit or line terminator, or last char of title, or last char of delimiter after title. 
                        int end;
                        if (this.header.Length - 1 > start)
                        {
                            // There are charaters after start index, so see if they are digits
                            if (char.IsDigit(this.header[start + 1]))
                            {
                                // They are indeed digits.  So set end index to last digit
                                end = this.header.LastIndexOfAny("0123456789".ToCharArray(), start + 1);
                            }
                            else
                            {
                                // No, they are not digits. So set end index same as start.
                                end = start;
                            }
                        }
                        else
                        {
                            // No characters after the start index so end index is same as start
                            end = start;
                        }

                        this.header = this.header.Substring(start) + this.Body.Length.ToString() + ((this.header.Length - 1 > end) ? this.header.Substring(end + 1, this.header.Length - end) : string.Empty);
                    }
                    else
                    {
                        // We dont already have a Content-Length header so add.
                        this.header += this.HeaderContentLengthTitle + this.HeaderItemDelimiter + (this.SpaceAfterHeaderItemDelimiter ? " " : string.Empty) + this.Body.Length.ToString() + this.HeaderItemLineTerminator;
                    }
                }

                // Bring top line (if we have one), header and body together in harmony.  With nice delimiters between them
                return ((topLine == null) ? string.Empty : topLine + this.HeaderItemLineTerminator) + this.header + this.HeaderBodyDelimiter + this.Body;
            }

            /// <summary>
            /// Builds HTTP Request Header top line using <see cref="httpMethod"/>, <see cref="resourcePath"/> and <see cref="query"/>.  Query string
            /// is delimited from Resource path using <see cref="HttpURLQuerySeparator"/>.
            /// </summary>
            /// <returns>Built top line of required HTTP Request</returns>
            private string BuildTopLine()
            {
                string topLine = string.Empty;
                if (this.httpMethod != null)
                {
                    switch (this.httpMethod)
                    {
                        case HTTPMethods.Post:
                            topLine = $"{this.HttpTypePostText}";
                            break;
                        case HTTPMethods.Get:
                            topLine = $"{this.HttpTypeGetText}";
                            break;
                        default: throw new ArgumentException("Must be POST or GET.  Others not yet implemented", "HTTPMethod");
                    }

                    // Add resource path (if we have one)
                    topLine += " " + ((this.resourcePath == string.Empty) ? string.Empty : this.resourcePath + " ");
                    
                    // Add query parameters (if we have any)
                    topLine += (this.query == string.Empty) ? string.Empty : this.HttpURLQuerySeparator + this.query + " ";
                    
                    // And finally, the HTTP version
                    topLine += this.HttpVersion;
                }

                return topLine;
            }
        }
    }
}