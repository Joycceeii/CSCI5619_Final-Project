using Newtonsoft.Json;

namespace WhoseStory
{
    // 模型每一轮返回的结构，与 json_schema 中的 character_reply 一一对应
    public class CharacterReply
    {
        [JsonProperty("reply")]
        public string Reply;

        [JsonProperty("lied")]
        public bool Lied;
    }
}
