using System.Collections.Generic;
using System.Text;

public class NormalInfoData {
    public string name;
    public long id;
}
public class SingleSongInfoData:NormalInfoData {
    public List<NormalInfoData> artists;
    public string getAllArtists() {
        string[] allArtists = new string[artists.Count];
        int count = 0;
        foreach (NormalInfoData data in artists) {
            if (count <= artists.Count - 1) { 
                allArtists[count] = data.name;
                count++;
            }
        }
        return string.Join(",", allArtists);
    }
}
public class RequestSongInfoData {
    public List<SingleSongInfoData> songs;
}