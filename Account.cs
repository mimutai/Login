using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;
using System.IO;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Login
{
    class Account
    {
        //IDのDictionaryを取得
        static public Dictionary<string,string> GetIDDictionary(string filePath)
        {
            //指定した.jsonファイルを取得する
            JsonData[] jsonDatas = JsonDeserialize(filePath);

            Dictionary<string, string> dict = new Dictionary<string, string>();

            foreach (JsonData jd in jsonDatas)
            {   
                //Keyに重複が無いことが前提
                dict.Add(jd.CardID, jd.PlayerID);
                Console.WriteLine("key= {0}, value= {1}", jd.CardID, jd.PlayerID);
            }

            return dict;
        }

        //Jsonの情報をデシリアライズするメソッド
        public static JsonData[] JsonDeserialize(string path)
        {

            StreamReader sr = new StreamReader(path);

            var serializer = new DataContractJsonSerializer(typeof(JsonData[]));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(sr.ReadToEnd())))
            {
                JsonData[] jd = (JsonData[])serializer.ReadObject(ms);
                return jd;
            }

        }

    }

    [DataContract]
    public class JsonData
    {
        [DataMember]
        public string CardID;
        [DataMember]
        public string PlayerID;
    }
}