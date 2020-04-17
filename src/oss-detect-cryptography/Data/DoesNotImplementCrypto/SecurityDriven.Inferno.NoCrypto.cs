internal static class AesConstants
{
    public const int AES_BLOCK_SIZE = 16;
    public const string STR_AES_BLOCK_SIZE = "16";
    public const int COUNTER_SIZE = 8;
    public const string STR_COUNTER_SIZE_IN_BITS = "64";
}

public AesCtrCryptoTransform(byte[] key, ArraySegment<byte> counterBufferSegment, Func<Aes> aesFactory = null)
{
    if (counterBufferSegment.Count != AesConstants.AES_BLOCK_SIZE)
        ThrowNewArgumentException("counterBufferSegment.Count must be " + AesConstants.STR_AES_BLOCK_SIZE + ".");

    var aes = this.aes = aesFactory == null ? AesFactories.Aes() : aesFactory();
    (aes.Mode, aes.Padding) = (CipherMode.ECB, PaddingMode.None);

    (var counterBufferSegmentArray, var counterBufferSegmentOffset) = (counterBufferSegment.Array, counterBufferSegment.Offset);
    System.Diagnostics.Debug.Assert(AesConstants.AES_BLOCK_SIZE == 16);

    (Unsafe.As<byte, ulong>(ref this.counterBuffer_KeyStreamBuffer[0]), this.counterStruct.UlongValue) =
        Unsafe.As<byte, (ulong, ulong)>(ref counterBufferSegmentArray[counterBufferSegmentOffset]);

    if (BitConverter.IsLittleEndian)
        this.counterStruct.UlongValue = Utils.ReverseEndianness(this.counterStruct.UlongValue);

    this.cryptoTransform = aes.CreateEncryptor(rgbKey: key, rgbIV: null);
}// ctor

public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
{
    byte[] outputBuffer = (inputCount == 0) ? Array.Empty<byte>() : new byte[inputCount];
    this.TransformBlock(inputBuffer, inputOffset, inputCount, outputBuffer, 0);
    this.Dispose();
    return outputBuffer;
}// TransformFinalBlock()

public void Dispose()
{
    var aes = this.aes;
    var cryptoTransform = this.cryptoTransform;

    if (aes != null) // null aes acts as "isDisposed" flag
    {
        try
        {
            cryptoTransform.Dispose();
            aes.Dispose();
        }
        finally
        {
            var counterBuffer_KeyStreamBuffer = this.counterBuffer_KeyStreamBuffer;
            Unsafe.InitBlock(ref counterBuffer_KeyStreamBuffer[AesConstants.AES_BLOCK_SIZE], 0, (uint)AesConstants.AES_BLOCK_SIZE);
            this.aes = null;
        }
    }// if aes is not null
}// Dispose()

public static class AesFactories
{
    internal static readonly Func<Aes> ManagedAes = () => new AesManaged();
    internal static readonly Func<Aes> FipsAes = Environment.OSVersion.Platform == PlatformID.Win32NT ?
        (Func<Aes>)(() => new AesCng()) :       // Windows
        () => new AesCryptoServiceProvider();   // non-Windows

    public static readonly Func<Aes> Aes = Utils.AllowOnlyFipsAlgorithms ? FipsAes : ManagedAes;
}//class AesFactories

public static string ToB64(this byte[] input)
{
    var inputAsArraySegment = input.AsArraySegment();
    return _ToB64(ref inputAsArraySegment);
}// ToB64()

static string _ToB64(ref ArraySegment<byte> inputSegment)
{
    byte[] inputArray = inputSegment.Array;
    int inputLength = inputSegment.Count;

    if (inputLength < 1)
        return String.Empty;

    int inputOffset = inputSegment.Offset;
    string base64Str = null;
    int endPos = 0;
    char[] base64Chars = null;

    ////////////////////////////////////////////////////////
    // Step 1: Do a Base64 encoding
    base64Str = Convert.ToBase64String(inputArray, inputOffset, inputLength);
    if (base64Str == null)
        return null;

    ////////////////////////////////////////////////////////
    // Step 2: Find how many padding chars are present in the end
    for (endPos = base64Str.Length; endPos > 0; endPos--)
    {
        if (base64Str[endPos - 1] != '=') // Found a non-padding char!
        {
            break; // Stop here
        }
    }

    ////////////////////////////////////////////////////////
    // Step 3: Create char array to store all non-padding chars,
    //      plus a char to indicate how many padding chars are needed
    base64Chars = new char[endPos + 1];
    base64Chars[endPos] = (char)((int)'0' + base64Str.Length - endPos); // Store a char at the end, to indicate how many padding chars are needed

    ////////////////////////////////////////////////////////
    // Step 3: Copy in the other chars. Transform the "+" to "-", and "/" to "_"
    for (int iter = 0; iter < endPos; iter++)
    {
        char c = base64Str[iter];

        switch (c)
        {
            case '+':
                base64Chars[iter] = '-';
                break;

            case '/':
                base64Chars[iter] = '_';
                break;

            case '=':
                System.Diagnostics.Debug.Assert(false);
                base64Chars[iter] = c;
                break;

            default:
                base64Chars[iter] = c;
                break;
        }
    }
    return new string(base64Chars);
}// _ToB64()

public static byte[] FromB64(this string input)
{
    if (input == null)
        throw new ArgumentNullException(nameof(input));

    int len = input.Length;
    if (len < 1)
        return Array.Empty<byte>();

    ///////////////////////////////////////////////////////////////////
    // Step 1: Calculate the number of padding chars to append to this string.
    //         The number of padding chars to append is stored in the last char of the string.
    int numPadChars = (int)input[len - 1] - (int)'0';
    if (numPadChars < 0 || numPadChars > 10)
        return null;

    ///////////////////////////////////////////////////////////////////
    // Step 2: Create array to store the chars (not including the last char)
    //          and the padding chars
    char[] base64Chars = new char[len - 1 + numPadChars];


    ////////////////////////////////////////////////////////
    // Step 3: Copy in the chars. Transform the "-" to "+", and "*" to "/"
    for (int iter = 0; iter < len - 1; iter++)
    {
        char c = input[iter];

        switch (c)
        {
            case '-':
                base64Chars[iter] = '+';
                break;

            case '_':
                base64Chars[iter] = '/';
                break;

            default:
                base64Chars[iter] = c;
                break;
        }
    }

    ////////////////////////////////////////////////////////
    // Step 4: Add padding chars
    for (int iter = len - 1; iter < base64Chars.Length; iter++)
    {
        base64Chars[iter] = '=';
    }

    // Do the actual conversion
    return Convert.FromBase64CharArray(base64Chars, 0, base64Chars.Length);
}// FromB64()

public static string ToB64Url(this byte[] input)
{
    var inputAsArraySegment = input.AsArraySegment();
    return _ToB64Url(ref inputAsArraySegment);
}// ToB64Url()

static string _ToB64Url(ref ArraySegment<byte> inputSegment)
{
    if (inputSegment.Count < 1) return string.Empty;
    string b64str = _ToB64(ref inputSegment);
    return b64str.Substring(0, b64str.Length - 1);
}// _ToB64Url()

public static byte[] FromB64Url(this string str)
{
    int lengthMod4 = str.Length % 4;
    string b64str = str + (lengthMod4 == 2 ? "2" : lengthMod4 == 3 ? "1" : "0");
    return b64str.FromB64();
}// FromB64Url()


public Base16Config(char[] alphabet = null)
{
    if (alphabet == null)
    {
        this.Base16table = HexUppercase.Base16table;
        this.ReverseMap = HexUppercase.ReverseMap;
        return;
    }

    if (alphabet.Length != BASE)
        throw new ArgumentOutOfRangeException(nameof(alphabet), $"'{nameof(alphabet)}' array must have exactly {BASE.ToString()} characters.");

    this.Base16table = alphabet;

    char ch;
    this.ReverseMap = new byte[byte.MaxValue];
    for (byte i = 0; i < Base16table.Length; ++i)
    {
        ch = Base16table[i];
        this.ReverseMap[char.ToUpperInvariant(ch)] = i;
        this.ReverseMap[char.ToLowerInvariant(ch)] = i;
    }
}//ctor

public override int GetHashCode()
{
    if (this.hashcode == null)
        this.hashcode = new string(this.Base16table).GetHashCode();
    return this.hashcode.GetValueOrDefault();
}

public override bool Equals(object obj)
{
    if (!(obj is Base16Config rhs))
        return false;

    if (this.GetHashCode() != rhs.GetHashCode())
        return false;

    for (int i = 0; i < BASE; ++i)
    {
        if (this.Base16table[i] != rhs.Base16table[i])
            return false;
    }
    return true;
}

public static string ToBase16(this byte[] binary, Base16Config config = null)
{
    if (config == null)
        config = Base16Config.HexUppercase;
    var base16table = config.Base16table;

    var chars = new char[binary.Length * 2];
    for (int i = 0, b; i < binary.Length; ++i)
    {
        b = binary[i];
        chars[i * 2] = base16table[b >> 4];
        chars[i * 2 + 1] = base16table[b & 0xF];
    }
    return new string(chars);
}//ToBase16()

public static string ToBase16(this ArraySegment<byte> binarySegment, Base16Config config = null)
{
    if (config == null)
        config = Base16Config.HexUppercase;
    var base16table = config.Base16table;

    byte[] binaryArray = binarySegment.Array;
    int binaryLength = binarySegment.Count;
    int binaryOffset = binarySegment.Offset;

    var chars = new char[binaryLength * 2];
    for (int i = 0, b; i < binaryLength; ++i)
    {
        b = binaryArray[binaryOffset + i];
        chars[i * 2] = base16table[b >> 4];
        chars[i * 2 + 1] = base16table[b & 0xF];
    }
    return new string(chars);
}//ToBase16()

public static byte[] FromBase16(this string str16, Base16Config config = null)
{
    if (config == null)
        config = Base16Config.HexUppercase;

    var reverseMap = config.ReverseMap;

    byte[] result = new byte[str16.Length / 2];
    for (int i = 0; i < result.Length; ++i)
    {
        result[i] = (byte)((reverseMap[str16[i * 2]] << 4) + reverseMap[str16[i * 2 + 1]]);
    }
    return result;
}//FromBase16

public Base32Config(char[] alphabet = null)
{
    if (alphabet == null)
    {
        this.Base32table = Default.Base32table;
        this.ReverseMap = Default.ReverseMap;
        return;
    }

    if (alphabet.Length != BASE)
        throw new ArgumentOutOfRangeException(nameof(alphabet), $"'{nameof(alphabet)}' array must have exactly {BASE.ToString()} characters.");

    this.Base32table = alphabet;

    char ch;
    this.ReverseMap = new long[byte.MaxValue];
    for (int i = 0; i < Base32table.Length; ++i)
    {
        ch = Base32table[i];
        this.ReverseMap[char.ToUpperInvariant(ch)] = i;
        this.ReverseMap[char.ToLowerInvariant(ch)] = i;
    }
}//ctor

public override int GetHashCode()
{
    if (this.hashcode == null)
        this.hashcode = new string(this.Base32table).GetHashCode();
    return this.hashcode.GetValueOrDefault();
}

public override bool Equals(object obj)
{
    if (!(obj is Base32Config rhs))
        return false;

    if (this.GetHashCode() != rhs.GetHashCode())
        return false;

    for (int i = 0; i < BASE; ++i)
    {
        if (this.Base32table[i] != rhs.Base32table[i])
            return false;
    }
    return true;
}

public static string ToBase32(this byte[] binary, Base32Config config = null)
{
    int length = binary.Length;
    int bitLength = checked(length * 8);
    int base32Length = bitLength / 5;
    if (base32Length * 5 != bitLength)
        throw new ArgumentOutOfRangeException(nameof(binary), $"'{nameof(binary)}' array length must be a multiple of 5.");

    if (config == null)
        config = Base32Config.Default;
    var base32table = config.Base32table;

    char[] chArray = new char[base32Length];
    for (int i = 0, num2Start = 7, num2 = num2Start, index, num4; i < length; i += 5, num2Start += 8, num2 = num2Start)
    {
        num4 = binary[i + 1] << 24 | binary[i + 2] << 16 | binary[i + 3] << 8 | binary[i + 4];

        index = num4 & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 5) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 10) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 15) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 20) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 25) & 31;
        chArray[num2--] = base32table[index];

        num4 = (num4 >> 30) & 3 | binary[i] << 2;
        index = num4 & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 5) & 31;
        chArray[num2--] = base32table[index];
    }
    return new string(chArray);
}//ToBase32()

public static string ToBase32(this ArraySegment<byte> binarySegment, Base32Config config = null)
{
    byte[] binaryArray = binarySegment.Array;
    int binaryLength = binarySegment.Count;
    int binaryOffset = binarySegment.Offset;

    int bitLength = checked(binaryLength * 8);
    int base32Length = bitLength / 5;
    if (base32Length * 5 != bitLength)
        throw new ArgumentOutOfRangeException(nameof(binarySegment), $"'{nameof(binarySegment)}' length must be a multiple of 5.");

    if (config == null)
        config = Base32Config.Default;
    var base32table = config.Base32table;

    char[] chArray = new char[base32Length];
    for (int i = 0, num2Start = 7, num2 = num2Start, index, num4; i < binaryLength; i += 5, num2Start += 8, num2 = num2Start)
    {
        num4 = binaryArray[binaryOffset + i + 1] << 24 | binaryArray[binaryOffset + i + 2] << 16 | binaryArray[binaryOffset + i + 3] << 8 | binaryArray[binaryOffset + i + 4];

        index = num4 & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 5) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 10) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 15) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 20) & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 25) & 31;
        chArray[num2--] = base32table[index];

        num4 = (num4 >> 30) & 3 | binaryArray[binaryOffset + i] << 2;
        index = num4 & 31;
        chArray[num2--] = base32table[index];

        index = (num4 >> 5) & 31;
        chArray[num2--] = base32table[index];
    }
    return new string(chArray);
}//ToBase32()

public static byte[] FromBase32(this string str32, Base32Config config = null)
{
    int length = str32.Length;
    int bit5length = length / 8;
    if (bit5length * 8 != length)
        throw new ArgumentOutOfRangeException(nameof(str32), $"'{nameof(str32)}' string length must be a multiple of 8.");

    if (config == null)
        config = Base32Config.Default;
    var reverseMap = config.ReverseMap;

    int byteLength = bit5length * 5;
    byte[] result = new byte[byteLength];

    long tmp;
    for (int i = 0, indexStart = 4, index = indexStart; i < length; i += 8, indexStart += 5, index = indexStart)
    {
        tmp =
            reverseMap[str32[i + 0]] << 35 |
            reverseMap[str32[i + 1]] << 30 |
            reverseMap[str32[i + 2]] << 25 |
            reverseMap[str32[i + 3]] << 20 |
            reverseMap[str32[i + 4]] << 15 |
            reverseMap[str32[i + 5]] << 10 |
            reverseMap[str32[i + 6]] << 5 |
            reverseMap[str32[i + 7]];

        result[index--] = (byte)tmp; tmp >>= 8;
        result[index--] = (byte)tmp; tmp >>= 8;
        result[index--] = (byte)tmp; tmp >>= 8;
        result[index--] = (byte)tmp; tmp >>= 8;
        result[index--] = (byte)tmp; tmp >>= 8;
    }
    return result;
}//FromBase32()

public static byte[] CloneBytes(this byte[] bytes, int offset, int count)
{
    var clone = new byte[count];

    if (count <= SHORT_BYTECOPY_THRESHOLD)
        for (int i = 0; i < count; ++i) clone[i] = bytes[offset + i];
    else
        Utils.BlockCopy(bytes, offset, clone, 0, count);

    return clone;
}//CloneBytes()

public static void ClearBytes(this byte[] bytes, int offset, int count)
{
    System.Array.Clear(bytes, offset, count);
}//ClearBytes()

public static CngKey CreateNewDhmKey(string name = null)
{
    return CngKey.Create(CngAlgorithm.ECDiffieHellmanP384, name, cngKeyCreationParameters);
}

public static CngKey CreateNewDsaKey(string name = null)
{
    return CngKey.Create(CngAlgorithm.ECDsaP384, name, cngKeyCreationParameters);
}

public static byte[] GetPrivateBlob(this CngKey key)
{
    return key.Export(CngKeyBlobFormat.EccPrivateBlob);
}

public static byte[] GetPublicBlob(this CngKey key)
{
    return key.Export(CngKeyBlobFormat.EccPublicBlob);
}
public static CngKey ToPrivateKeyFromBlob(this byte[] privateBlob)
{
    var key = CngKey.Import(privateBlob, CngKeyBlobFormat.EccPrivateBlob);
    key.SetProperty(exportPolicy_AllowPlaintextExport);
    return key;
}

public static CngKey ToPublicKeyFromBlob(this byte[] publicBlob)
{
    return CngKey.Import(publicBlob, CngKeyBlobFormat.EccPublicBlob);
}

public static byte[] GetSharedDhmSecret(this CngKey privateDhmKey, CngKey publicDhmKey, byte[] contextAppend = null, byte[] contextPrepend = null)
{
#if (NET462 || NETCOREAPP2_1)
			using (var ecdh = new ECDiffieHellmanCng(privateDhmKey) { HashAlgorithm = CngAlgorithm.Sha384, SecretAppend = contextAppend, SecretPrepend = contextPrepend })
				return ecdh.DeriveKeyMaterial(publicDhmKey);
#elif NETSTANDARD2_0
			throw new PlatformNotSupportedException($"ECDiffieHellman is not supported on .NET Standard 2.0. Please reference \"{typeof(CngKeyExtensions).Assembly.GetName().Name}\" from .NET Framework or .NET Core for ECDiffieHellman support.");
#else
#error Unknown target
#endif
}// GetSharedDhmSecret()

public static SharedEphemeralBundle GetSharedEphemeralDhmSecret(this CngKey receiverDhmPublicKey, byte[] contextAppend = null, byte[] contextPrepend = null)
{
    using (var sender = CreateNewDhmKey())
        return new SharedEphemeralBundle { SharedSecret = sender.GetSharedDhmSecret(receiverDhmPublicKey, contextAppend, contextPrepend), EphemeralDhmPublicKeyBlob = sender.GetPublicBlob() };
}

void Internal_Dispose()
{
    var sharedSecret = this.SharedSecret;
    if (sharedSecret != null)
    {
        Array.Clear(sharedSecret, 0, sharedSecret.Length);
        this.SharedSecret = null;
    }
}// Internal_Dispose()

public void Dispose()
{
    GC.SuppressFinalize(this);
    this.Internal_Dispose();
}// Dispose()

public static byte[] ToBytes(this string str)
{
    var length = str.Length;
    byte[] bytes = new byte[length * 2];
    char c;
    for (int i = 0; i < length; ++i)
    {
        c = str[i];
        bytes[i * 2] = (byte)c;
        bytes[i * 2 + 1] = (byte)(c >> 8);
    }
    return bytes;
}//ToBytes()

public static string FromBytes(this byte[] bytes)
{
    int byteCount = bytes.Length;
    if (byteCount % 2 != 0)
        throw new ArgumentException($"'{nameof(bytes)}' array must have even number of bytes", nameof(bytes));

    char[] chars = new char[byteCount / 2];
    for (int i = 0; i < chars.Length; ++i)
    {
        chars[i] = (char)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
    }
    return new String(chars);
}//FromBytes()

public static string FromBytes(this ArraySegment<byte> bytesSegment)
{
    byte[] bytesArray = bytesSegment.Array;
    int bytesLength = bytesSegment.Count;
    int bytesOffset = bytesSegment.Offset;

    if (bytesLength % 2 != 0)
        throw new ArgumentException($"'{nameof(bytesSegment)}' must have even number of bytes", nameof(bytesSegment));

    char[] chars = new char[bytesLength / 2];
    for (int i = 0; i < chars.Length; ++i)
    {
        chars[i] = (char)(bytesArray[bytesOffset + i * 2] | (bytesArray[bytesOffset + i * 2 + 1] << 8));
    }
    return new String(chars);
}//FromBytes()

public static class HashFactories
{
    static readonly Func<SHA1> ManagedSHA1 = () => new SHA1Managed();
    static readonly Func<SHA1> FipsSHA1 =
#if NET462
			() => new SHA1Cng();
#else
            () => System.Security.Cryptography.SHA1.Create();
#endif
    static readonly Func<SHA256> ManagedSHA256 = () => new SHA256Managed();
    static readonly Func<SHA256> FipsSHA256 =
#if NET462
			() => new SHA256Cng();
#else
            () => System.Security.Cryptography.SHA256.Create();
#endif
    static readonly Func<SHA384> ManagedSHA384 = () => new SHA384Managed();
    static readonly Func<SHA384> FipsSHA384 =
#if NET462
			() => new SHA384Cng();
#else
            () => System.Security.Cryptography.SHA384.Create();
#endif

    static readonly Func<SHA512> ManagedSHA512 = () => new SHA512Managed();
    static readonly Func<SHA512> FipsSHA512 =
#if NET462
			() => new SHA512Cng();
#else
            () => System.Security.Cryptography.SHA512.Create();
#endif

    internal static readonly Func<SHA1> SHA1 = Utils.AllowOnlyFipsAlgorithms ? FipsSHA1 : ManagedSHA1;
    public static readonly Func<SHA256> SHA256 = Utils.AllowOnlyFipsAlgorithms ? FipsSHA256 : ManagedSHA256;
    public static readonly Func<SHA384> SHA384 = Utils.AllowOnlyFipsAlgorithms ? FipsSHA384 : ManagedSHA384;
    public static readonly Func<SHA512> SHA512 = Utils.AllowOnlyFipsAlgorithms ? FipsSHA512 : ManagedSHA512;
}// HashFactories class

public HKDF(Func<HMAC> hmacFactory, byte[] ikm, byte[] salt = null, byte[] context = null)
{
    hmac = hmacFactory();
    hmac2 = hmac as HMAC2;
    hashLength = hmac.HashSize >> 3;

    // a malicious implementation of HMAC could conceivably mess up the shared static empty byte arrays, which are still writeable...
    hmac.Key = salt ?? (hashLength == 48 ? emptyArray48 : hashLength == 64 ? emptyArray64 : hashLength == 32 ? emptyArray32 : hashLength == 20 ? emptyArray20 : new byte[hashLength]);

    // re-keying hmac with PRK
    hmac.TransformBlock(ikm, 0, ikm.Length, null, 0);
    hmac.TransformFinalBlock(ikm, 0, 0);
    hmac.Key = (hmac2 != null) ? hmac2.HashInner : hmac.Hash;
    hmac.Initialize();
    this.context = context;
    Reset();
}

public override void Reset()
{
    k = Array.Empty<byte>();
    k_unused = 0;
    counter = 0;
}


protected override void Dispose(bool disposing)
{
    var hmac = this.hmac;
    if (hmac != null)
    {
        hmac.Dispose();
        this.hmac = null;
    }
}

public PBKDF2(Func<HMAC> hmacFactory, byte[] password, byte[] salt, int iterations)
{
    this.Salt = salt;
    this.IterationCount = iterations;
    this.hmac = hmacFactory();
    this.hmac2 = hmac as HMAC2;
    this.hmac.Key = password;
    this.BlockSize = hmac.HashSize / 8;
    this.Initialize();
}//ctor


static byte[] GenerateSalt(int saltSize)
{
    if (saltSize < 0)
        throw new ArgumentOutOfRangeException(nameof(saltSize));

    byte[] data = new byte[saltSize];
    rng.NextBytes(data, 0, data.Length);
    return data;
}//GenerateSalt()

public int IterationCount
{
    get
    {
        return this.iterations;
    }
    set
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        this.iterations = value;
        this.Initialize();
    }
}
public byte[] Salt
{
    get
    {
        return this.salt.CloneBytes();
    }
    set
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (value.Length < 8)
        {
            throw new ArgumentException("Salt is not at least 8 bytes.");
        }
        this.salt = value.CloneBytes();
        this.Initialize();
    }
}

void Initialize()
{
    if (this.buffer != null)
    {
        Array.Clear(this.buffer, 0, this.buffer.Length);
    }
    this.buffer = new byte[BlockSize];
    this.block = 1;
    this.startIndex = this.endIndex = 0;
}

public override void Reset()
{
    this.Initialize();
}

internal static byte[] CreateBuffer(ref ArraySegment<byte>? label, ref ArraySegment<byte>? context, uint keyLengthInBits)
{
    int labelLength = label?.Count ?? 0;
    int contextLength = context?.Count ?? 0;
    int bufferLength = (COUNTER_LENGTH /* counter */) + (labelLength + 1 /* label + 0x00 */) + (contextLength /* context */) + (DERIVED_KEY_LENGTH_LENGTH /* [L]_2 */);
    var buffer = new byte[bufferLength];

    // store label, if any
    if (labelLength > 0)
    {
        var labelSegment = label.GetValueOrDefault();
        var labelSegmentArray = labelSegment.Array;
        var labelSegmentOffset = labelSegment.Offset;

        if (labelLength > Extensions.ByteArrayExtensions.SHORT_BYTECOPY_THRESHOLD)
            Utils.BlockCopy(labelSegmentArray, labelSegmentOffset, buffer, COUNTER_LENGTH, labelLength);
        else
            for (int i = 0; i < labelLength; ++i) buffer[COUNTER_LENGTH + i] = labelSegmentArray[labelSegmentOffset + i];
    }

    // store context, if any
    if (contextLength > 0)
    {
        var contextSegment = context.GetValueOrDefault();
        var contextSegmentArray = contextSegment.Array;
        var contextSegmentOffset = contextSegment.Offset;

        if (contextLength > Extensions.ByteArrayExtensions.SHORT_BYTECOPY_THRESHOLD)
            Utils.BlockCopy(contextSegment.Array, contextSegment.Offset, buffer, COUNTER_LENGTH + labelLength + 1, contextLength);
        else
            for (int i = 0; i < contextLength; ++i) buffer[COUNTER_LENGTH + labelLength + 1 + i] = contextSegmentArray[contextSegmentOffset + i];
    }

    // store key length
    new Utils.IntStruct { UintValue = keyLengthInBits }.ToBEBytes(buffer, bufferLength - DERIVED_KEY_LENGTH_LENGTH);
    return buffer;
}// CreateBuffer()

public static void DeriveKey(Func<HMAC> hmacFactory, byte[] key, ArraySegment<byte>? label, ArraySegment<byte>? context, ArraySegment<byte> derivedOutput, uint counter = 1)
{
    using (var hmac = hmacFactory())
    {
        hmac.Key = key;
        var buffer = CreateBuffer(label: ref label, context: ref context, keyLengthInBits: checked((uint)(derivedOutput.Count << 3)));
        DeriveKey(hmac, buffer, ref derivedOutput, counter);
    }
}// DeriveKey()

public HMAC2(Func<HashAlgorithm> hashFactory)
{
    hashAlgorithm = hashFactory();
    base.HashSizeValue = hashAlgorithm.HashSize;
#if NET462
			if (hashAlgorithm is SHA384Cng || hashAlgorithm is SHA512Cng || hashAlgorithm is SHA384 || hashAlgorithm is SHA512)
#else
    if (hashAlgorithm is SHA384 || hashAlgorithm is SHA512)
#endif
    {
        base.BlockSizeValue = blockSizeValue;
    }
    else blockSizeValue = 64;

    // [block-sized raw key value] || [ipad xor'ed into block-sized raw key value] || [opad xor'ed into block-sized raw key value]
    base.KeyValue = new byte[blockSizeValue * 3];
}// ctor

public new string HashName
{
    get
    {
        return hashAlgorithm.ToString();
    }
    set
    {
        throw new NotSupportedException("Do not set underlying hash algorithm via 'HashName' property - use constructor instead.");
    }
}// HashName

protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        if (hashAlgorithm != null)
        {
            hashAlgorithm.Dispose(); // will also clear base.HashValue
            hashAlgorithm = null;
        }

        if (base.KeyValue != null)
        {
            Unsafe.InitBlock(ref this.KeyValue[0], 0, (uint)this.KeyValue.Length);//Array.Clear(this.KeyValue, 0, this.KeyValue.Length);
            this.KeyValue = null;
        }
    }
    // intentionally do not call base.Dispose(disposing)
}// Dispose()

public override byte[] Hash
{
    get
    {
        if (hashAlgorithm == null) throw new ObjectDisposedException(nameof(hashAlgorithm));
        if (base.State != 0) throw new CryptographicUnexpectedOperationException("Hash must be finalized before the hash value is retrieved.");

        var hashValue = base.HashValue;
        var hashValueClone = new byte[hashValue.Length];
        for (int i = 0; i < hashValueClone.Length; ++i) hashValueClone[i] = hashValue[i];
        return hashValueClone;
    }
}// Hash

public override void Initialize()
{
    hashAlgorithm.Initialize();
    isHashing = false;
    isHashDirty = false;
}// Initialize()

public byte[] HashInner
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get { return base.HashValue; }
}// HashInner

static long GetCurrentTimeDeltaInTicks(Func<DateTime> timeFactory)
{
    var time = timeFactory();
    if (time.Kind == DateTimeKind.Local) throw new ArgumentException("DateTime cannot of 'Local' kind.", nameof(timeFactory));
    if (time < _unixEpoch) throw new ArgumentOutOfRangeException(nameof(timeFactory), $"DateTime cannot be less than {_unixEpoch.ToString()}.");
    long deltaTicks = (time - _unixEpoch).Ticks;
    return deltaTicks;
}//GetCurrentTimeDeltaInTicks()

static long GetCurrentTimeStepNumber(Func<DateTime> timeFactory)
{
    var deltaTicks = GetCurrentTimeDeltaInTicks(timeFactory);
    var timeStepNumber = deltaTicks / _timeStepTicks;
    return timeStepNumber;
}//GetCurrentTimeStepNumber()

public static DateTime GetExpiryTime(Func<DateTime> timeFactory = null)
{
    timeFactory ??= _timeFactory;
    long nextTimeStep = checked(GetCurrentTimeStepNumber(timeFactory) + 1);
    return _unixEpoch.AddTicks(checked(nextTimeStep * _timeStepTicks));
}//GetExpiryTime()

public static int GenerateTOTP(byte[] secret, Func<DateTime> timeFactory = null, int totpLength = DEFAULT_TOTP_LENGTH, string modifier = null)
{
    timeFactory ??= _timeFactory;
    long currentTimeStep = GetCurrentTimeStepNumber(timeFactory);

    return ComputeTotp(secret, currentTimeStep, totpLength, modifier);
}//GenerateTOTP()

static CryptoRandom()
{
    SanityCheck();
}// static ctor

public CryptoRandom() : base(Seed: 0)
{
    // Minimize the wasted time of calling default System.Random base ctor.
    // We can't avoid calling at least some base ctor, ie. 2~3 milliseconds are wasted anyway.
    // That's the price of inheriting from System.Random (doesn't implement an interface).
}// ctor

static void SanityCheck()
{
    var testBuffer = new byte[BYTE_CACHE_SIZE / 2];
    int status, i, j;
    const int COLLISION_FREE_BLOCK_SIZE = 16;

    status = (int)BCrypt.BCryptGenRandom(testBuffer, testBuffer.Length);
    if (status != (int)BCrypt.NTSTATUS.STATUS_SUCCESS) ThrowNewCryptographicException(status);

    if (testBuffer.Length < COLLISION_FREE_BLOCK_SIZE * 2) return; // should be compiled away
    for (i = 0; i < testBuffer.Length - COLLISION_FREE_BLOCK_SIZE; i += COLLISION_FREE_BLOCK_SIZE)
    {
        for (j = 0, status = 0; j < COLLISION_FREE_BLOCK_SIZE; ++j)
            status |= testBuffer[i + j] ^ testBuffer[i + j + COLLISION_FREE_BLOCK_SIZE];
        if (status == 0) ThrowNewCryptographicException("CryptoRandom failed sanity check #2.");
    }
}// SanityCheck()

#region NextLong()
/// <summary>
/// Returns a nonnegative random number.
/// </summary>
/// <returns>
/// A 64-bit signed integer greater than or equal to zero and less than <see cref="F:System.Int64.MaxValue"/>.
/// </returns>
public long NextLong()
{
    // Mask away the sign bit so that we always return nonnegative integers
    return GetRandomLong() & 0x7FFFFFFFFFFFFFFF;
}//NextLong()

/// <summary>
/// Returns a nonnegative random number less than the specified maximum.
/// </summary>
/// <param name="maxValue">The exclusive upper bound of the random number to be generated. <paramref name="maxValue"/> must be greater than or equal to zero.</param>
/// <returns>
/// A 64-bit signed integer greater than or equal to zero, and less than <paramref name="maxValue"/>; that is, the range of return values ordinarily includes zero but not <paramref name="maxValue"/>. However, if <paramref name="maxValue"/> equals zero, <paramref name="maxValue"/> is returned.
/// </returns>
/// <exception cref="T:System.ArgumentOutOfRangeException">
///     <paramref name="maxValue"/> is less than zero.
/// </exception>
public long NextLong(long maxValue)
{
    if (maxValue < 0)
        ThrowNewArgumentOutOfRangeException(nameof(maxValue));

    return NextLong(0, maxValue);
}//NextLong()


#endregion

#region Next()
/// <summary>
/// Returns a nonnegative random number.
/// </summary>
/// <returns>
/// A 32-bit signed integer greater than or equal to zero and less than <see cref="F:System.Int32.MaxValue"/>.
/// </returns>
public override int Next()
{
    // Mask away the sign bit so that we always return nonnegative integers
    return GetRandomInt() & 0x7FFFFFFF;
}//Next()

/// <summary>
/// Returns a nonnegative random number less than the specified maximum.
/// </summary>
/// <param name="maxValue">The exclusive upper bound of the random number to be generated. <paramref name="maxValue"/> must be greater than or equal to zero.</param>
/// <returns>
/// A 32-bit signed integer greater than or equal to zero, and less than <paramref name="maxValue"/>; that is, the range of return values ordinarily includes zero but not <paramref name="maxValue"/>. However, if <paramref name="maxValue"/> equals zero, <paramref name="maxValue"/> is returned.
/// </returns>
/// <exception cref="T:System.ArgumentOutOfRangeException">
///     <paramref name="maxValue"/> is less than zero.
/// </exception>
public override int Next(int maxValue)
{
    if (maxValue < 0)
        ThrowNewArgumentOutOfRangeException(nameof(maxValue));

    return Next(0, maxValue);
}//Next()

/// <summary>
/// Returns a random number within a specified range.
/// </summary>
/// <param name="minValue">The inclusive lower bound of the random number returned.</param>
/// <param name="maxValue">The exclusive upper bound of the random number returned. <paramref name="maxValue"/> must be greater than or equal to <paramref name="minValue"/>.</param>
/// <returns>
/// A 32-bit signed integer greater than or equal to <paramref name="minValue"/> and less than <paramref name="maxValue"/>; that is, the range of return values includes <paramref name="minValue"/> but not <paramref name="maxValue"/>. If <paramref name="minValue"/> equals <paramref name="maxValue"/>, <paramref name="minValue"/> is returned.
/// </returns>
/// <exception cref="T:System.ArgumentOutOfRangeException">
///     <paramref name="minValue"/> is greater than <paramref name="maxValue"/>.
/// </exception>
public override int Next(int minValue, int maxValue)
{
    if (minValue == maxValue) return minValue;
    if (minValue > maxValue) ThrowNewArgumentOutOfRangeException(nameof(minValue));

    // new logic, based on 
    // https://github.com/dotnet/corefx/blob/067f6a6c4139b2991db1c1e49152b0a86df3fdb2/src/System.Security.Cryptography.Algorithms/src/System/Security/Cryptography/RandomNumberGenerator.cs#L100

    uint range = (uint)(maxValue - minValue) - 1;

    // If there is only one possible choice, nothing random will actually happen, so return the only possibility.
    if (range == 0) return minValue;

    // Create a mask for the bits that we care about for the range. The other bits will be masked away.
    uint mask = range;
    mask |= mask >> 01;
    mask |= mask >> 02;
    mask |= mask >> 04;
    mask |= mask >> 08;
    mask |= mask >> 16;

    uint result;
    do
    {
        result = (uint)GetRandomInt() & mask;
    } while (result > range);
    return minValue + (int)result;
}//Next()
#endregion

/// <summary>
/// Returns a random number between 0.0 and 1.0.
/// </summary>
/// <returns>
/// A double-precision floating point number greater than or equal to 0.0, and less than 1.0.
/// </returns>
public override double NextDouble()
{
    const double max = 1L << 53; // https://en.wikipedia.org/wiki/Double-precision_floating-point_format
    return ((ulong)GetRandomLong() >> 11) / max;
}//NextDouble()

/// <summary>
/// Returns a new count-sized byte array filled with random bytes.
/// </summary>
/// <param name="count">Array length.</param>
/// <returns>Random byte array.</returns>
public byte[] NextBytes(int count)
{
    byte[] bytes = new byte[count];
    this.NextBytes(bytes, 0, count);
    return bytes;
}//NextBytes()

// Inherited from Random. We must override this one to prevent inherited Random.NextBytes from ever getting called.
// Not overriding the inherited "NextBytes" and instead hiding it via "public new NextBytes(buffer)"
// would create a security vulnerability - don't be tempted.
/// <summary>
/// Fills the elements of a specified array of bytes with random numbers.
/// Use "NextBytes(buffer,offset,count)" for a bit more performance (non-virtual).
/// </summary>
/// <param name="buffer">The array to fill with cryptographically strong random bytes.</param>
/// <exception cref="T:System.ArgumentNullException">
///     <paramref name="buffer"/> is null.
/// </exception>
public override void NextBytes(byte[] buffer) => NextBytes(buffer, 0, buffer.Length);

/// <summary>
/// Fills the specified byte array with a cryptographically strong random sequence of values.
/// </summary>
/// <param name="buffer">An array of bytes to contain random numbers.</param>
/// <param name="offset"></param>
/// <param name="count">Number of bytes to generate (must be lte buffer.Length).</param>
/// <exception cref="T:System.ArgumentNullException">
///     <paramref name="buffer"/> is null.
/// </exception>
public void NextBytes(byte[] buffer, int offset, int count)
{
    new ArraySegment<byte>(buffer, offset, count); // bounds-validation happens here
    if (count == 0) return;
    NextBytesInternal(buffer, offset, count);
}//NextBytes()

void NextBytesInternal(byte[] buffer, int offset, int count)
{
    BCrypt.NTSTATUS status;

    if (count > CACHE_THRESHOLD)
    {
        status = (offset == 0) ? BCrypt.BCryptGenRandom(buffer, count) : BCrypt.BCryptGenRandom_WithOffset(buffer, offset, count);
        if (status == BCrypt.NTSTATUS.STATUS_SUCCESS) return;
        ThrowNewCryptographicException((int)status);
    }

    lock (_byteCache)
    {
        if (_byteCachePosition + count <= BYTE_CACHE_SIZE)
        {
            Utils.BlockCopy(_byteCache, _byteCachePosition, buffer, offset, count);
            _byteCachePosition += count;
            return;
        }

        status = BCrypt.BCryptGenRandom(_byteCache, BYTE_CACHE_SIZE);
        if (status == BCrypt.NTSTATUS.STATUS_SUCCESS)
        {
            _byteCachePosition = count;
            Utils.BlockCopy(_byteCache, 0, buffer, offset, count);
            return;
        }
        ThrowNewCryptographicException((int)status);
    }// lock
}//NextBytesInternal()

/// <summary>
/// Gets one random signed 32bit integer in a thread safe manner.
/// </summary>
int GetRandomInt()
{
    lock (_byteCache)
    {
        if (_byteCachePosition + sizeof(int) <= BYTE_CACHE_SIZE)
        {
            var result = Unsafe.As<byte, int>(ref _byteCache[_byteCachePosition]);//BitConverter.ToInt32(_byteCache, _byteCachePosition);
            _byteCachePosition += sizeof(int);
            return result;
        }

        BCrypt.NTSTATUS status = BCrypt.BCryptGenRandom(_byteCache, BYTE_CACHE_SIZE);
        if (status == BCrypt.NTSTATUS.STATUS_SUCCESS)
        {
            _byteCachePosition = sizeof(int);
            return Unsafe.As<byte, int>(ref _byteCache[0]);//BitConverter.ToInt32(_byteCache, 0);
        }
        return ThrowNewCryptographicException((int)status);
    }// lock
}//GetRandomInt()

/// <summary>
/// Gets one random signed 64bit integer in a thread safe manner.
/// </summary>
long GetRandomLong()
{
    lock (_byteCache)
    {
        if (_byteCachePosition + sizeof(long) <= BYTE_CACHE_SIZE)
        {
            var result = Unsafe.As<byte, long>(ref _byteCache[_byteCachePosition]);//BitConverter.ToInt64(_byteCache, _byteCachePosition);
            _byteCachePosition += sizeof(long);
            return result;
        }

        BCrypt.NTSTATUS status = BCrypt.BCryptGenRandom(_byteCache, BYTE_CACHE_SIZE);
        if (status == BCrypt.NTSTATUS.STATUS_SUCCESS)
        {
            _byteCachePosition = sizeof(long);
            return Unsafe.As<byte, long>(ref _byteCache[0]);//BitConverter.ToInt64(_byteCache, 0);
        }
        return ThrowNewCryptographicException((int)status);
    }// lock
}//GetRandomLong()

public static int CalculateCiphertextLength(ArraySegment<byte> plaintext)
{
    int finalBlockLength = plaintext.Count - (plaintext.Count & (-Cipher.AesConstants.AES_BLOCK_SIZE));
    int paddingLength = AES_IV_LENGTH - finalBlockLength;
    return CONTEXT_BUFFER_LENGTH + plaintext.Count + paddingLength + MAC_LENGTH;
}

static void ValidateAes(Aes aes) // detect & fix any Mode/Padding deviation
{
    if (aes.Mode == CipherMode.CBC && aes.Padding == PaddingMode.PKCS7) return;
    aes.Mode = CipherMode.CBC;
    aes.Padding = PaddingMode.PKCS7;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
static void ClearKeyMaterial()
{
    Array.Clear(_encKey.Value, 0, ENC_KEY_LENGTH);
    Array.Clear(_macKey.Value, 0, MAC_KEY_LENGTH);
    Array.Clear(_sessionKey.Value, 0, HMAC_LENGTH);
}

static void ClearKeyMaterial(byte[] encKey, byte[] macKey, byte[] sessionKey)
{
    for (int i = 0; i < encKey.Length; ++i) encKey[i] = 0;  //Array.Clear(encKey, 0, encKey.Length);			
    for (int j = 0; j < macKey.Length; ++j) macKey[j] = 0;  //Array.Clear(macKey, 0, macKey.Length);
    Array.Clear(sessionKey, 0, sessionKey.Length);
}// ClearKeyMaterial()

internal static void PrependSaltWith1stBlockContext(ref ArraySegment<byte>? salt, byte[] contextBuffer, int contextBufferOffset)
{
    int saltLength = salt?.Count ?? 0;
    var streamContextCombinedWithSalt = new byte[EtM_CTR.CONTEXT_TWEAK_LENGTH + saltLength];

    for (int i = EtM_CTR.CONTEXT_TWEAK_LENGTH - 1; i >= 0; --i)
    {
        streamContextCombinedWithSalt[i] = contextBuffer[contextBufferOffset + i];
    }

    if (saltLength > 0)
    {
        var saltValue = salt.GetValueOrDefault();
        Utils.BlockCopy(saltValue.Array, saltValue.Offset, streamContextCombinedWithSalt, EtM_CTR.CONTEXT_TWEAK_LENGTH, saltLength);
    }
    salt = new ArraySegment<byte>(streamContextCombinedWithSalt);
}// PrependSaltWith1stBlockContext()

/// <summary>ctor</summary>
public EtM_EncryptTransform(byte[] key, ArraySegment<byte>? salt = null)
{
    if (key == null) throw new ArgumentNullException(nameof(key), nameof(key) + " cannot be null.");
    this.key = key;
    this.salt = salt;
    this.currentChunkNumber = EtM_Transform_Constants.INITIAL_CHUNK_NUMBER;
}
public EtM_DecryptTransform(byte[] key, ArraySegment<byte>? salt = null, bool authenticateOnly = false)
{
    if (key == null) throw new ArgumentNullException(nameof(key), nameof(key) + " cannot be null.");
    this.key = key;
    this.salt = salt;
    this.currentChunkNumber = EtM_Transform_Constants.INITIAL_CHUNK_NUMBER;
    this.IsAuthenticateOnly = authenticateOnly;
}

public static byte[] Encrypt(byte[] masterKey, ArraySegment<byte> plaintext, ArraySegment<byte>? salt = null)
{
    return EtM_CTR.Encrypt(masterKey, plaintext, salt);
}

public static byte[] Decrypt(byte[] masterKey, ArraySegment<byte> ciphertext, ArraySegment<byte>? salt = null)
{
    return EtM_CTR.Decrypt(masterKey, ciphertext, salt);
}

public static bool Authenticate(byte[] masterKey, ArraySegment<byte> ciphertext, ArraySegment<byte>? salt = null)
{
    return EtM_CTR.Authenticate(masterKey, ciphertext, salt);
}
internal static Action<T, V> CreateSetter<T, V>(this FieldInfo field)
{
    var targetExp = Expression.Parameter(typeof(T));
    var valueExp = Expression.Parameter(typeof(V));

    // Expression.Property can be used here as well
    var fieldExp = Expression.Field(targetExp, field);
    var assignExp = Expression.Assign(fieldExp, valueExp);

    var setter = Expression.Lambda<Action<T, V>>(assignExp, targetExp, valueExp).Compile();
    return setter;
}// CreateSetter()
#endregion

#region CreateGetter<T,V>
internal static Func<T, V> CreateGetter<T, V>(this FieldInfo field)
{
    var targetExp = Expression.Parameter(typeof(T));

    // Expression.Property can be used here as well
    var fieldExp = Expression.Field(targetExp, field);

    var getter = Expression.Lambda<Func<T, V>>(fieldExp, targetExp).Compile();
    return getter;
}// CreateGetter()
#endregion

#region ConstantTimeEqual() - byte arrays & ArraySegments
/// <exception cref="System.NullReferenceException">Thrown when either array is null.</exception>
[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
public static bool ConstantTimeEqual(byte[] x, int xOffset, byte[] y, int yOffset, int length)
{
    // based on https://github.com/CodesInChaos/Chaos.NaCl/blob/55e84738252932fa123eaa7bb0dd9cb99de0ceb9/Chaos.NaCl/CryptoBytes.cs (public domain)
    // another sanity reference: https://golang.org/src/crypto/subtle/constant_time.go
    // Null checks of "x" and "y" are skipped. Appropriate exceptions will be raised anyway.

    if (xOffset < 0)
        throw new ArgumentOutOfRangeException("xOffset", "xOffset < 0");
    if (yOffset < 0)
        throw new ArgumentOutOfRangeException("yOffset", "yOffset < 0");
    if (length < 0)
        throw new ArgumentOutOfRangeException("length", "length < 0");
    if (checked(xOffset + length) > x.Length)
        throw new ArgumentException("xOffset + length > x.Length");
    if (checked(yOffset + length) > y.Length)
        throw new ArgumentException("yOffset + length > y.Length");

    int differentbits = 0;
    unchecked
    {
        for (int i = 0; i < length; ++i)
        {
            differentbits |= x[xOffset + i] ^ y[yOffset + i];
        }
    }
    return differentbits == 0;
}// ConstantTimeEqual()

public static bool ConstantTimeEqual(ArraySegment<byte> x, ArraySegment<byte> y)
{
    int xCount = x.Count;
    if (xCount != y.Count)
        throw new ArgumentException("x.Count must equal y.Count");

    return ConstantTimeEqual(x.Array, x.Offset, y.Array, y.Offset, xCount);
}// ConstantTimeEqual()

/// <exception cref="System.NullReferenceException">Thrown when either array is null.</exception>
public static bool ConstantTimeEqual(byte[] x, byte[] y)
{
    int xLength = x.Length;
    if (xLength != y.Length)
        throw new ArgumentException("x.Length must equal y.Length");

    return ConstantTimeEqual(x, 0, y, 0, xLength);
}// ConstantTimeEqual()
#endregion

#region ConstantTimeEqual() - strings
[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
static bool ConstantTimeEqual(string x, int xOffset, string y, int yOffset, int length)
{
    // Null checks of "x" and "y" are skipped. Appropriate exceptions will be raised anyway.
    if (xOffset < 0)
        throw new ArgumentOutOfRangeException("xOffset", "xOffset < 0");
    if (yOffset < 0)
        throw new ArgumentOutOfRangeException("yOffset", "yOffset < 0");
    if (length < 0)
        throw new ArgumentOutOfRangeException("length", "length < 0");
    if (checked(xOffset + length) > x.Length)
        throw new ArgumentException("xOffset + length > x.Length");
    if (checked(yOffset + length) > y.Length)
        throw new ArgumentException("yOffset + length > y.Length");

    int differentbits = 0;
    unchecked
    {
        for (int i = 0; i < length; ++i)
        {
            differentbits |= x[xOffset + i] ^ y[yOffset + i];
        }
    }
    return differentbits == 0;
}// ConstantTimeEqual()

/// <exception cref="System.NullReferenceException">Thrown when either string is null.</exception>
public static bool ConstantTimeEqual(string x, string y)
{
    int xLength = x.Length;
    if (xLength != y.Length)
        throw new ArgumentException("x.Length must equal y.Length");

    return ConstantTimeEqual(x, 0, y, 0, xLength);
}// ConstantTimeEqual()
#endregion

#region IntStruct
[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal struct IntStruct
{
    [FieldOffset(0)]
    public int IntValue;
    [FieldOffset(0)]
    public uint UintValue;

    [FieldOffset(0)]
    public byte B1;
    [FieldOffset(1)]
    public byte B2;
    [FieldOffset(2)]
    public byte B3;
    [FieldOffset(3)]
    public byte B4;

    /// <summary>
    /// To Big-Endian
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ToBEBytes(byte[] buffer, int offset = 0)
    {
        if (BitConverter.IsLittleEndian)
        {
            Unsafe.As<byte, uint>(ref buffer[offset]) = Utils.ReverseEndianness(UintValue);
        }
        else
        {
            Unsafe.As<byte, uint>(ref buffer[offset]) = UintValue;
        }
    }// ToBEBytes()
}// IntStruct
#endregion

#region LongStruct
[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal struct LongStruct
{
    [FieldOffset(0)]
    public long LongValue;
    [FieldOffset(0)]
    public ulong UlongValue;

    [FieldOffset(0)]
    public byte B1;
    [FieldOffset(1)]
    public byte B2;
    [FieldOffset(2)]
    public byte B3;
    [FieldOffset(3)]
    public byte B4;
    [FieldOffset(4)]
    public byte B5;
    [FieldOffset(5)]
    public byte B6;
    [FieldOffset(6)]
    public byte B7;
    [FieldOffset(7)]
    public byte B8;

    /// <summary>
    /// To Big-Endian
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ToBEBytes(byte[] buffer, int offset = 0)
    {
        if (BitConverter.IsLittleEndian)
        {
            Unsafe.As<byte, ulong>(ref buffer[offset]) = Utils.ReverseEndianness(UlongValue);
        }
        else
        {
            Unsafe.As<byte, ulong>(ref buffer[offset]) = UlongValue;
        }
    }// ToBEBytes()
}// LongStruct
#endregion

internal static readonly Action<Array, int, Array, int, int> BlockCopy = Buffer.BlockCopy;

#region Combine byte arrays & segments
public static byte[] Combine(ArraySegment<byte> a, ArraySegment<byte> b)
{
    byte[] combinedArray = new byte[checked(a.Count + b.Count)];
    BlockCopy(a.Array, a.Offset, combinedArray, 0, a.Count);
    BlockCopy(b.Array, b.Offset, combinedArray, a.Count, b.Count);
    return combinedArray;
}// Combine(two byte array segments)

public static byte[] Combine(byte[] a, byte[] b) { return Combine(a.AsArraySegment(), b.AsArraySegment()); }// Combine(two byte arrays)

public static byte[] Combine(ArraySegment<byte> a, ArraySegment<byte> b, ArraySegment<byte> c)
{
    byte[] combinedArray = new byte[checked(a.Count + b.Count + c.Count)];
    BlockCopy(a.Array, a.Offset, combinedArray, 0, a.Count);
    BlockCopy(b.Array, b.Offset, combinedArray, a.Count, b.Count);
    BlockCopy(c.Array, c.Offset, combinedArray, a.Count + b.Count, c.Count);
    return combinedArray;
}// Combine(three byte array segments)

public static byte[] Combine(byte[] a, byte[] b, byte[] c) { return Combine(a.AsArraySegment(), b.AsArraySegment(), c.AsArraySegment()); }// Combine(three byte arrays)

public static byte[] Combine(params byte[][] arrays)
{
    int combinedArrayLength = 0, combinedArrayOffset = 0;
    for (int i = 0; i < arrays.Length; ++i) checked { combinedArrayLength += arrays[i].Length; }
    byte[] array, combinedArray = new byte[combinedArrayLength];

    for (int i = 0; i < arrays.Length; ++i)
    {
        array = arrays[i];
        BlockCopy(array, 0, combinedArray, combinedArrayOffset, array.Length);
        combinedArrayOffset += array.Length;
    }
    return combinedArray;
}// Combine(params byte[][])

public static byte[] Combine(params ArraySegment<byte>[] arraySegments)
{
    int combinedArrayLength = 0, combinedArrayOffset = 0;
    for (int i = 0; i < arraySegments.Length; ++i) checked { combinedArrayLength += arraySegments[i].Count; }
    byte[] combinedArray = new byte[combinedArrayLength];

    for (int i = 0; i < arraySegments.Length; ++i)
    {
        var segment = arraySegments[i];
        BlockCopy(segment.Array, segment.Offset, combinedArray, combinedArrayOffset, segment.Count);
        combinedArrayOffset += segment.Count;
    }
    return combinedArray;
}// Combine(params ArraySegment<byte>[])
#endregion

#region Xor
[StructLayout(LayoutKind.Explicit, Pack = 0)]
internal struct Union
{
    [FieldOffset(0)]
    public byte[] Bytes;

    [FieldOffset(0)]
    public long[] Longs;
}// struct Union

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Xor(byte[] dest, int destOffset, byte[] left, int leftOffset, byte[] right, int rightOffset, int byteCount)
{
    int i = 0;
    if ((byteCount > Extensions.ByteArrayExtensions.SHORT_BYTECOPY_THRESHOLD) && (((destOffset | leftOffset | rightOffset) & 7) == 0)) // all offsets must be multiples of 8 for long-sized xor
    {
        long[] destUnionLongs = new Union { Bytes = dest }.Longs, leftUnionLongs = new Union { Bytes = left }.Longs, rightUnionLongs = new Union { Bytes = right }.Longs;
        int longDestOffset = destOffset >> 3, longLeftOffset = leftOffset >> 3, longRightOffset = rightOffset >> 3, longCount = byteCount >> 3;
        for (; i < longCount; ++i) destUnionLongs[longDestOffset + i] = leftUnionLongs[longLeftOffset + i] ^ rightUnionLongs[longRightOffset + i];
        i = longCount << 3;
    }
    for (; i < byteCount; ++i) dest[destOffset + i] = (byte)(left[leftOffset + i] ^ right[rightOffset + i]);
}// Xor()

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Xor(byte[] dest, int destOffset, byte[] left, int leftOffset, int byteCount)
{
    int i = 0;
    if ((byteCount > Extensions.ByteArrayExtensions.SHORT_BYTECOPY_THRESHOLD) && (((destOffset | leftOffset) & 7) == 0)) // all offsets must be multiples of 8 for long-sized xor
    {
        long[] destUnionLongs = new Union { Bytes = dest }.Longs, leftUnionLongs = new Union { Bytes = left }.Longs;
        int longDestOffset = destOffset >> 3, longLeftOffset = leftOffset >> 3, longCount = byteCount >> 3;
        for (; i < longCount; ++i) destUnionLongs[longDestOffset + i] ^= leftUnionLongs[longLeftOffset + i];
        i = longCount << 3;
    }
    for (; i < byteCount; ++i) dest[destOffset + i] ^= left[leftOffset + i];
}// Xor()
#endregion

#region ReverseEndianness
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static ulong ReverseEndianness(ulong value)
{
    return ((ulong)ReverseEndianness((uint)value) << 32) + ReverseEndianness((uint)(value >> 32));
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static uint ReverseEndianness(uint value)
{
    (uint xx_zz, uint ww_yy) = (value & 0x00FF00FF, value & 0xFF00FF00);
    return ((xx_zz >> 8) | (xx_zz << 24)) | ((ww_yy << 8) | (ww_yy >> 24));
}
