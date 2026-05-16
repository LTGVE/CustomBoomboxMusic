using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;



public class URLProcess
{

    public static URLData ProcessURL(string url)
    {
        Debug.Log("Processing URL: " + url);
        Uri uri = new Uri(url);
        bool isHttp = uri.Scheme == Uri.UriSchemeHttp;
        string domain = uri.Host;
        string path = uri.AbsolutePath;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var queryData = new List<QueryData>();
        for (int i = 0; i < query.Count; i++) { 
            var key = query.AllKeys[i];
            var value = query[i];
            queryData.Add(new QueryData(key, value));
            
        }
        return new URLData(isHttp,domain, path, queryData);
    }


}

