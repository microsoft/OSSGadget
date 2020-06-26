using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoGatherer
{
    public enum CodeLanguage
    {
        Csharp,
        C_Cpp,
        Go,
        Java,
        JavaScript,
        PHP,
        Python,
        Ruby,
        Rust
    }

    public enum CryptoAlgorithm
    {
        Asymmetric_RSA,
        Hash_BCrypt,
        Hash_Blake,
        Hash_Blake2,
        Hash_Blake3,
        Hash_GOST,
        Hash_Grøstl,
        Hash_HAS_160,
        Hash_HAVAL,
        Hash_JH,
        Hash_Kupyna,
        Hash_MD2,
        Hash_MD4,
        Hash_MD5,
        Hash_MD6,
        Hash_RIPEMD,
        Hash_SHA_1,
        Hash_SHA_2_224,
        Hash_SHA_2_256,
        Hash_SHA_2_384,
        Hash_SHA_2_512,
        Hash_SHA3,
        Hash_Siphash,
        Hash_Skein,
        Hash_SM3,
        Hash_Snefru,
        Hash_SpectralHash,
        Hash_Streebog,
        Hash_Tiger,
        Hash_Whirlpool,
        KeyAgreement_Curve25519_X25519,
        PRNG,
        Symmetric_AES,
        Symmetric_Salsa20_ChaCha
    }
}
