

using System.Collections.Generic;

public class URLData
{
    public bool isHttp { get; private set; }
    public string Domain { get; private set; }
    public string Path { get; private set; }
    public List<QueryData> queryData { get; private set; }
    public URLData(bool isHttp, string domain, string path, List<QueryData> queryData) { 
        this.isHttp = isHttp;
        this.Domain = domain;
        this.Path = path;
        this.queryData = queryData;
    }
}
public class QueryData
{
    public string key { get; private set; }
    public string value { get; private set; }
    public QueryData(string key, string value)
    {
        this.key = key;
        this.value = value;
    }

}

