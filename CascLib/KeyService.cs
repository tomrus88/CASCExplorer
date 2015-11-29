using System.Collections.Generic;

namespace CASCExplorer
{
    class KeyService
    {
        private static Dictionary<ulong, byte[]> keys = new Dictionary<ulong, byte[]>()
        {
            // hardcoded Overwatch keys
            [0x402CD9D8D6BFED98] = "AEB0EADEA47612FE6C041A03958DF241".ToByteArray(),
            [0xFB680CB6A8BF81F3] = "62D90EFA7F36D71C398AE2F1FE37BDB9".ToByteArray(),
            // streamed Overwatch keys
            [0xDBD3371554F60306] = "34E397ACE6DD30EEFDC98A2AB093CD3C".ToByteArray(),
            [0x11A9203C9881710A] = "2E2CB8C397C2F24ED0B5E452F18DC267".ToByteArray(),
            [0xA19C4F859F6EFA54] = "0196CB6F5ECBAD7CB5283891B9712B4B".ToByteArray(),
            [0x87AEBBC9C4E6B601] = "685E86C6063DFDA6C9E85298076B3D42".ToByteArray(),
            [0xDEE3A0521EFF6F03] = "AD740CE3FFFF9231468126985708E1B9".ToByteArray(),
            [0x8C9106108AA84F07] = "53D859DDA2635A38DC32E72B11B32F29".ToByteArray(),
            [0x49166D358A34D815] = "667868CD94EA0135B9B16C93B1124ABA".ToByteArray(),
            // streamed WoW keys
            [0xB76729641141CB34] = "9849D1AA7B1FD09819C5C66283A326EC".ToByteArray(),
            [0x23C5B5DF837A226C] = "1406E2D873B6FC99217A180881DA8D62".ToByteArray(),
            [0xD1E9B5EDF9283668] = "8E4A2579894E38B4AB9058BA5C7328EE".ToByteArray(),

            // 3ECB6A12785050FA - BDC51862ABED79B2DE48C8E7E66C6200
            // ? - AA0B5C77F088CCC2D39049BD267F066D
            // ? - D514BD1909A9E5DC8703F4B8BB1DFD9A
        };

        private static Salsa20 salsa = new Salsa20();

        public static Salsa20 SalsaInstance
        {
            get { return salsa; }
        }

        public static byte[] GetKey(ulong keyName)
        {
            byte[] key;
            keys.TryGetValue(keyName, out key);
            return key;
        }
    }
}
