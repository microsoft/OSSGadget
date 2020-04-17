public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
{
    if (inputCount == 0) return 0;

    int i, j, remainingInputCount = inputCount;
    byte[] counterBuffer_KeyStreamBuffer = this.counterBuffer_KeyStreamBuffer; // looks dumb, but local-access is faster than field-access

    // process any available key stream first
    if (this.keyStreamBytesRemaining > 0)
    {
        j = inputCount > this.keyStreamBytesRemaining ? this.keyStreamBytesRemaining : inputCount;
        for (i = 0; i < j; ++i)
            outputBuffer[outputOffset + i] = (byte)(counterBuffer_KeyStreamBuffer[AesConstants.AES_BLOCK_SIZE * 2 - this.keyStreamBytesRemaining + i] ^ inputBuffer[inputOffset + i]);

        this.keyStreamBytesRemaining -= j;
        remainingInputCount -= j;
        if (remainingInputCount == 0) return inputCount;

        inputOffset += j;
        outputOffset += j;
    }

    int fullBlockSize = (remainingInputCount >> 4) << 4;
    int partialBlockSize = remainingInputCount - fullBlockSize;

    ref var _counterStructRef = ref this.counterStruct;

    // process full blocks, if any
    if (fullBlockSize > 0)
    {
        ref ulong counterHalf1 = ref Unsafe.As<byte, ulong>(ref counterBuffer_KeyStreamBuffer[0]);
        if (BitConverter.IsLittleEndian)
        {
            for (i = outputOffset, /* reusing j as iMax */ j = outputOffset + fullBlockSize; i < j; i += AesConstants.AES_BLOCK_SIZE)
                Unsafe.As<byte, (ulong, ulong)>(ref outputBuffer[i]) = (counterHalf1, Utils.ReverseEndianness(_counterStructRef.UlongValue++));
        }
        else
        {
            for (i = outputOffset, /* reusing j as iMax */ j = outputOffset + fullBlockSize; i < j; i += AesConstants.AES_BLOCK_SIZE)
                Unsafe.As<byte, (ulong, ulong)>(ref outputBuffer[i]) = (counterHalf1, _counterStructRef.UlongValue++);
        }

        fullBlockSize = this.cryptoTransform.TransformBlock(outputBuffer, outputOffset, fullBlockSize, outputBuffer, outputOffset);
        i = 0;

        const bool VECTORIZE = true;
        if (VECTORIZE)
        {   // vectorized xor
            int vectorLength_x2 = VECTOR_LENGTH << 1;
            int vectorLength_x4 = VECTOR_LENGTH << 2;

            int wideVectorLength;
            int vectorLimit;

            wideVectorLength = vectorLength_x4;
            vectorLimit = fullBlockSize - wideVectorLength;

            for (; i <= vectorLimit; i += wideVectorLength)
            {
                ref var destVectors = ref Unsafe.As<byte, (Vector<byte>, Vector<byte>, Vector<byte>, Vector<byte>)>(ref outputBuffer[outputOffset + i]);
                ref var leftVectors = ref Unsafe.As<byte, (Vector<byte>, Vector<byte>, Vector<byte>, Vector<byte>)>(ref inputBuffer[inputOffset + i]);

                destVectors.Item4 ^= leftVectors.Item4;
                destVectors.Item3 ^= leftVectors.Item3;
                destVectors.Item2 ^= leftVectors.Item2;
                destVectors.Item1 ^= leftVectors.Item1;
            }

            wideVectorLength = vectorLength_x2;
            vectorLimit = fullBlockSize - wideVectorLength;

            for (; i <= vectorLimit; i += wideVectorLength)
            {
                ref var destVectors = ref Unsafe.As<byte, (Vector<byte>, Vector<byte>)>(ref outputBuffer[outputOffset + i]);
                ref var leftVectors = ref Unsafe.As<byte, (Vector<byte>, Vector<byte>)>(ref inputBuffer[inputOffset + i]);

                destVectors.Item2 ^= leftVectors.Item2;
                destVectors.Item1 ^= leftVectors.Item1;
            }
        }//if (VECTORIZE)
        for (; i < fullBlockSize; ++i) outputBuffer[outputOffset + i] ^= inputBuffer[inputOffset + i];
    }// if fullBlockSize > 0

    // process the remaining partial block, if any
    if (partialBlockSize > 0)
    {
        inputOffset += fullBlockSize;
        outputOffset += fullBlockSize;

        if (BitConverter.IsLittleEndian)
        { /**/ Unsafe.As<byte, ulong>(ref counterBuffer_KeyStreamBuffer[8]) = Utils.ReverseEndianness(_counterStructRef.UlongValue++); }
        else { Unsafe.As<byte, ulong>(ref counterBuffer_KeyStreamBuffer[8]) = _counterStructRef.UlongValue++; }

        this.cryptoTransform.TransformBlock(counterBuffer_KeyStreamBuffer, 0, AesConstants.AES_BLOCK_SIZE, counterBuffer_KeyStreamBuffer, AesConstants.AES_BLOCK_SIZE);

        for (i = 0; i < partialBlockSize; ++i) outputBuffer[outputOffset + i] = (byte)(counterBuffer_KeyStreamBuffer[AesConstants.AES_BLOCK_SIZE + i] ^ inputBuffer[inputOffset + i]);
        this.keyStreamBytesRemaining = AesConstants.AES_BLOCK_SIZE - partialBlockSize;
    }//if partialBlockSize > 0

    return inputCount;
}// TransformBlock()

public override byte[] GetBytes(int countBytes)
{
    var okm = new byte[countBytes];
    if (k_unused > 0)
    {
        var min = k_unused > countBytes ? countBytes : k_unused;//Math.Min(k_unused, countBytes);
        Utils.BlockCopy(k, hashLength - k_unused, okm, 0, min);
        countBytes -= min;
        k_unused -= min;
    }
    if (countBytes == 0) return okm;

    int n = countBytes / hashLength + 1;
    int contextLength = context != null ? context.Length : 0;
    byte[] hmac_msg = new byte[hashLength + contextLength + 1];

    for (var i = 1; i <= n; ++i)
    {
        Utils.BlockCopy(k, 0, hmac_msg, 0, k.Length);
        if (contextLength > 0)
            Utils.BlockCopy(context, 0, hmac_msg, k.Length, contextLength);

        hmac_msg[k.Length + contextLength] = checked(++counter);

        if (hmac2 != null)
        {
            hmac2.TransformBlock(hmac_msg, 0, k.Length + contextLength + 1, null, 0);
            hmac2.TransformFinalBlock(hmac_msg, 0, 0);
            k = hmac2.HashInner;
        }
        else
            k = hmac.ComputeHash(hmac_msg, 0, k.Length + contextLength + 1);

        Utils.BlockCopy(k, 0, okm, okm.Length - countBytes, i < n ? hashLength : countBytes);
        countBytes -= hashLength;
    }
    k_unused = -countBytes;
    return okm;
}// GetBytes()

byte[] Func()
{
    // localize members
    var _inputBuffer = this.inputBuffer;
    var _hmac = this.hmac;
    var _hmac2 = this.hmac2;

    new Utils.IntStruct { UintValue = this.block }.ToBEBytes(_inputBuffer);
    this.hmac.TransformBlock(inputBuffer: this.salt, inputOffset: 0, inputCount: this.salt.Length, outputBuffer: null, outputOffset: 0);
    this.hmac.TransformBlock(inputBuffer: _inputBuffer, inputOffset: 0, inputCount: _inputBuffer.Length, outputBuffer: null, outputOffset: 0);
    this.hmac.TransformFinalBlock(inputBuffer: _inputBuffer, inputOffset: 0, inputCount: 0);
    byte[] hash = this.hmac.Hash; // creates a copy
    this.hmac.Initialize();
    byte[] buffer3 = hash;

    for (int i = 2, blockSize = BlockSize, j = this.iterations; i <= j; i++)
    {
        if (_hmac2 != null)
        {
            _hmac2.TransformBlock(hash, 0, blockSize, null, 0);
            _hmac2.TransformFinalBlock(hash, 0, 0);
            hash = _hmac2.HashInner;
        }
        else hash = _hmac.ComputeHash(hash);
        Utils.Xor(dest: buffer3, destOffset: 0, left: hash, leftOffset: 0, byteCount: blockSize);
    }
    this.block++;
    return buffer3;
}

public override byte[] GetBytes(int cb)
{
    if (cb <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(cb), "Positive number required.");
    }
    byte[] dst = new byte[cb];
    int dstOffsetBytes = 0;
    int byteCount = this.endIndex - this.startIndex;
    if (byteCount > 0)
    {
        if (cb < byteCount)
        {
            Buffer.BlockCopy(this.buffer, this.startIndex, dst, 0, cb);
            this.startIndex += cb;
            return dst;
        }
        Buffer.BlockCopy(this.buffer, this.startIndex, dst, 0, byteCount);
        this.startIndex = this.endIndex = 0;
        dstOffsetBytes += byteCount;
    }//if

    while (dstOffsetBytes < cb)
    {
        byte[] src = this.Func();
        int num3 = cb - dstOffsetBytes;
        if (num3 > BlockSize)
        {
            Buffer.BlockCopy(src, 0, dst, dstOffsetBytes, BlockSize);
            dstOffsetBytes += BlockSize;
        }
        else
        {
            Buffer.BlockCopy(src, 0, dst, dstOffsetBytes, num3);
            dstOffsetBytes += num3;
            Buffer.BlockCopy(src, num3, this.buffer, this.startIndex, BlockSize - num3);
            this.endIndex += BlockSize - num3;
            return dst;
        }
    }//while
    return dst;
}//GetBytes()

internal static void DeriveKey(HMAC keyedHmac, byte[] bufferArray, ref ArraySegment<byte> derivedOutput, uint counter = 1)
{
    int derivedOutputCount = derivedOutput.Count, derivedOutputOffset = derivedOutput.Offset;
    var derivedOutputArray = derivedOutput.Array;
    byte[] K_i = null;
    HMAC2 keyedHmac2 = keyedHmac as HMAC2;
    checked
    {
        // Calculate each K_i value and copy the leftmost bits to the output buffer as appropriate.
        for (var counterStruct = new Utils.IntStruct { UintValue = counter }; derivedOutputCount > 0; ++counterStruct.UintValue)
        {
            counterStruct.ToBEBytes(bufferArray, 0); // update the counter within the buffer

            if (keyedHmac2 == null)
            {
                K_i = keyedHmac.ComputeHash(bufferArray);
            }
            else
            {
                keyedHmac2.TransformBlock(bufferArray, 0, bufferArray.Length, null, 0);
                keyedHmac2.TransformFinalBlock(bufferArray, 0, 0);
                K_i = keyedHmac2.HashInner;
            }

            // copy the leftmost bits of K_i into the output buffer
            int numBytesToCopy = derivedOutputCount > K_i.Length ? K_i.Length : derivedOutputCount;//Math.Min(derivedOutputCount, K_i.Length);

            //Utils.BlockCopy(K_i, 0, derivedOutput.Array, derivedOutputOffset, numBytesToCopy);
            for (int i = 0; i < numBytesToCopy; ++i) derivedOutputArray[derivedOutputOffset + i] = K_i[i];

            derivedOutputOffset += numBytesToCopy;
            derivedOutputCount -= numBytesToCopy;
        }// for
    }// checked
    if (keyedHmac2 == null && K_i != null) Array.Clear(K_i, 0, K_i.Length); /* clean up needed only when HMAC implementation is not HMAC2 */
}// DeriveKey()

public override byte[] Key
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get
    {
        var keyValueClone = new byte[keyLength];
        for (int i = 0; i < keyValueClone.Length; ++i) keyValueClone[i] = base.KeyValue[i];
        return keyValueClone;
    }
    set
    {
        if (isHashing) throw new CryptographicException("Hash key cannot be changed after the first write to the stream.");
        if (isHashDirty) { hashAlgorithm.Initialize(); isHashDirty = false; }

        var blockSizeValue = this.blockSizeValue;

        if (value.Length > blockSizeValue)
        {
            hashAlgorithm.TransformBlock(value, 0, value.Length, null, 0);
            hashAlgorithm.TransformFinalBlock(value, 0, 0);
            value = _HashValue_Getter(hashAlgorithm);

            hashAlgorithm.Initialize();
        }
        var keyLength = value.Length;
        this.keyLength = keyLength;

        var keyValue = base.KeyValue;

        Utils.BlockCopy(value, 0, keyValue, 0, keyLength);
        //for (int i = 0; i < keyLength; ++i) keyValue[i] = value[i];

        if (isRekeying) Array.Clear(keyValue, keyLength, blockSizeValue - keyLength);
        else isRekeying = true;

        //Utils.Xor(dest: keyValue, destOffset: blockSizeValue, left: ipad, leftOffset: 0, right: keyValue, rightOffset: 0, byteCount: blockSizeValue);
        //Utils.Xor(dest: keyValue, destOffset: blockSizeValue << 1, left: opad, leftOffset: 0, right: keyValue, rightOffset: 0, byteCount: blockSizeValue);

        long[] keyValueLongs = new Utils.Union { Bytes = keyValue }.Longs, ipadLongs = _ipadLongs, opadLongs = _opadLongs;
        blockSizeValue = blockSizeValue >> 3;

        for (int i = 0, blockSizeValue_x2 = blockSizeValue << 1; i < blockSizeValue; ++i)
        {
            keyValueLongs[blockSizeValue + i] = keyValueLongs[i] ^ ipadLongs[i];
            keyValueLongs[blockSizeValue_x2 + i] = keyValueLongs[i] ^ opadLongs[i];
        }
    }//setter
}// Key

protected override void HashCore(byte[] rgb, int ib, int cb)
{
    if (isHashDirty) { hashAlgorithm.Initialize(); isHashDirty = false; }
    if (isHashing == false)
    {
        hashAlgorithm.TransformBlock(base.KeyValue, blockSizeValue, blockSizeValue, null, 0);
        isHashing = true;
    }
    hashAlgorithm.TransformBlock(rgb, ib, cb, null, 0);
}// HashCore()

protected override byte[] HashFinal()
{
    if (isHashDirty) { hashAlgorithm.Initialize(); } else isHashDirty = true;
    if (isHashing == false) hashAlgorithm.TransformBlock(base.KeyValue, blockSizeValue, blockSizeValue, null, 0);
    else isHashing = false;

    // finalize the original hash
    hashAlgorithm.TransformFinalBlock(base.KeyValue, 0, 0);
    byte[] innerHash = _HashValue_Getter(hashAlgorithm);

    hashAlgorithm.Initialize();

    hashAlgorithm.TransformBlock(base.KeyValue, blockSizeValue << 1, blockSizeValue, null, 0);
    hashAlgorithm.TransformBlock(innerHash, 0, innerHash.Length, null, 0);
    hashAlgorithm.TransformFinalBlock(innerHash, 0, 0);

    return (base.HashValue = _HashValue_Getter(hashAlgorithm));
}// HashFinal()

static int ComputeTotp(byte[] secret, long timeStepNumber, int totpLength, string modifier)
{
    if (secret == null) throw new ArgumentNullException(nameof(secret));

    byte[] timestepAsBytes = new byte[sizeof(long)], hash = null;
    new Utils.LongStruct { LongValue = timeStepNumber }.ToBEBytes(timestepAsBytes);

    using (var hmac = _hmacFactory())
    {
        hmac.Key = secret;

        hmac.TransformBlock(timestepAsBytes, 0, timestepAsBytes.Length, null, 0);
        if (!string.IsNullOrEmpty(modifier))
        {
            byte[] modifierbytes = modifier.ToBytes();
            hmac.TransformBlock(modifierbytes, 0, modifierbytes.Length, null, 0);
        }
        hmac.TransformFinalBlock(timestepAsBytes, 0, 0);
        hash = hmac.HashInner; // do not dispose hmac before 'hash' access --> will zero-out internal array

        // Generate dynamically-truncated string
        var offset = hash[hash.Length - 1] & 0x0F;
        Debug.Assert(offset + 4 < hash.Length);
        var binaryCode = (hash[offset] & 0x7F) << 24
                         | (hash[offset + 1]) << 16
                         | (hash[offset + 2]) << 8
                         | (hash[offset + 3]);

        return binaryCode % _totpModulos[totpLength];
    }//using
}//ComputeTotp()

public static bool ValidateTOTP(byte[] secret, int totp, Func<DateTime> timeFactory = null, int totpLength = DEFAULT_TOTP_LENGTH, string modifier = null)
{
    timeFactory ??= _timeFactory;
    long currentTimeStep = GetCurrentTimeStepNumber(timeFactory);

    bool result = false;
    for (int i = -1; i <= 1; ++i)
    {
        int computedTotp = ComputeTotp(secret, currentTimeStep + i, totpLength, modifier);
        result |= totp == computedTotp;
    }
    return result;
}//ValidateTOTP()
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static NTSTATUS BCryptGenRandom(byte[] pbBuffer, int cbBuffer)
{
    Debug.Assert(pbBuffer != null);
    Debug.Assert(cbBuffer >= 0 && cbBuffer <= pbBuffer.Length);
    return BCryptGenRandom(default, pbBuffer, cbBuffer, BCRYPT_USE_SYSTEM_PREFERRED_RNG);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static NTSTATUS BCryptGenRandom_WithOffset(byte[] pbBuffer, int obBuffer, int cbBuffer)
{
    Debug.Assert(pbBuffer != null);
    Debug.Assert(cbBuffer >= 0 && obBuffer >= 0 && (obBuffer + cbBuffer) <= pbBuffer.Length);
    return BCryptGenRandom(default, ref pbBuffer[obBuffer], cbBuffer, BCRYPT_USE_SYSTEM_PREFERRED_RNG);
}

internal static NTSTATUS BCryptGenRandom_PinnedBuffer(byte[] pbBuffer, int obBuffer, int cbBuffer)
{
    Debug.Assert(pbBuffer != null);
    Debug.Assert(cbBuffer >= 0 && obBuffer >= 0 && (obBuffer + cbBuffer) <= pbBuffer.Length);

    GCHandle pinnedBufferHandle = default;
    NTSTATUS status;
    try
    {
        pinnedBufferHandle = GCHandle.Alloc(pbBuffer, GCHandleType.Pinned);
        status = BCrypt.BCryptGenRandom(default, pinnedBufferHandle.AddrOfPinnedObject() + obBuffer, cbBuffer, BCRYPT_USE_SYSTEM_PREFERRED_RNG);
    }
    finally
    {
        if (pinnedBufferHandle.IsAllocated) pinnedBufferHandle.Free();
    }

    return status;
}// BCryptGenRandom()

public static void Encrypt(byte[] masterKey, ArraySegment<byte> plaintext, byte[] output, int outputOffset, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int fullBlockLength = plaintext.Count & (-Cipher.AesConstants.AES_BLOCK_SIZE);
    int finalBlockLength = plaintext.Count % Cipher.AesConstants.AES_BLOCK_SIZE;
    int paddingLength = Cipher.AesConstants.AES_BLOCK_SIZE - finalBlockLength;
    int ciphertextLength = CONTEXT_BUFFER_LENGTH + plaintext.Count + paddingLength + MAC_LENGTH;
    if (output.Length - outputOffset < ciphertextLength) throw new ArgumentOutOfRangeException(nameof(output), $"'{nameof(output)}' array segment is not big enough for the ciphertext");

    try
    {
        var iv = _iv.Value;
        var contextBuffer = _contextBuffer.Value;
        var encKey = _encKey.Value;
        var macKey = _macKey.Value;
        var sessionKey = _sessionKey.Value;

        using (var aes = _aesFactory())
        {
            EtM_CBC.ValidateAes(aes);
            _cryptoRandom.NextBytes(contextBuffer, 0, CONTEXT_BUFFER_LENGTH);

            Utils.BlockCopy(contextBuffer, CONTEXT_TWEAK_LENGTH, iv, 0, AES_IV_LENGTH);
            Kdf.SP800_108_Ctr.DeriveKey(hmacFactory: _hmacFactory, key: masterKey, label: salt, context: contextBuffer.AsArraySegment(), derivedOutput: sessionKey.AsArraySegment(), counter: counter);

            Utils.BlockCopy(sessionKey, 0, macKey, 0, MAC_KEY_LENGTH);
            Utils.BlockCopy(sessionKey, MAC_KEY_LENGTH, encKey, 0, ENC_KEY_LENGTH);
            Utils.BlockCopy(contextBuffer, 0, output, outputOffset, CONTEXT_BUFFER_LENGTH);
            using (var aesEncryptor = aes.CreateEncryptor(encKey, iv))
            {
                if (fullBlockLength > 0)
                    aesEncryptor.TransformBlock(inputBuffer: plaintext.Array, inputOffset: plaintext.Offset, inputCount: fullBlockLength, outputBuffer: output, outputOffset: outputOffset + CONTEXT_BUFFER_LENGTH);

                var finalBlockBuffer = aesEncryptor.TransformFinalBlock(inputBuffer: plaintext.Array, inputOffset: plaintext.Offset + fullBlockLength, inputCount: finalBlockLength);
                Utils.BlockCopy(finalBlockBuffer, 0, output, outputOffset + CONTEXT_BUFFER_LENGTH + fullBlockLength, finalBlockBuffer.Length);
            }// using aesEncryptor
        }// using aes

        using (var hmac = _hmacFactory())
        {
            hmac.Key = macKey;
            hmac.TransformBlock(output, outputOffset + CONTEXT_TWEAK_LENGTH, AES_IV_LENGTH + plaintext.Count + paddingLength, null, 0);
            hmac.TransformFinalBlock(output, 0, 0);
            var fullmac = hmac.HashInner;
            Utils.BlockCopy(fullmac, 0, output, outputOffset + ciphertextLength - MAC_LENGTH, MAC_LENGTH);
        }// using hmac
    }
    finally { EtM_CBC.ClearKeyMaterial(); }
}// Encrypt()

public static byte[] Encrypt(byte[] masterKey, ArraySegment<byte> plaintext, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int fullBlockLength = plaintext.Count & (-Cipher.AesConstants.AES_BLOCK_SIZE);
    int finalBlockLength = plaintext.Count % Cipher.AesConstants.AES_BLOCK_SIZE;
    int paddingLength = Cipher.AesConstants.AES_BLOCK_SIZE - finalBlockLength;
    int ciphertextLength = CONTEXT_BUFFER_LENGTH + plaintext.Count + paddingLength + MAC_LENGTH;
    byte[] buffer = new byte[ciphertextLength];
    EtM_CBC.Encrypt(masterKey: masterKey, plaintext: plaintext, output: buffer, outputOffset: 0, salt: salt, counter: counter);
    return buffer;
}// Encrypt()


public static void Decrypt(byte[] masterKey, ArraySegment<byte> ciphertext, ref ArraySegment<byte>? outputSegment, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int cipherLength = ciphertext.Count - CONTEXT_BUFFER_LENGTH - MAC_LENGTH;
    if (cipherLength < Cipher.AesConstants.AES_BLOCK_SIZE) { outputSegment = null; return; }
    int fullBlockLength = cipherLength - AES_IV_LENGTH;
    byte[] finalBlock = null;
    try
    {
        var iv = _iv.Value;
        var encKey = _encKey.Value;
        var macKey = _macKey.Value;
        var sessionKey = _sessionKey.Value;

        Kdf.SP800_108_Ctr.DeriveKey(hmacFactory: _hmacFactory, key: masterKey, label: salt, context: new ArraySegment<byte>(ciphertext.Array, ciphertext.Offset, CONTEXT_BUFFER_LENGTH), derivedOutput: sessionKey.AsArraySegment(), counter: counter);
        Utils.BlockCopy(sessionKey, 0, macKey, 0, MAC_KEY_LENGTH);

        using (var hmac = _hmacFactory())
        {
            hmac.Key = macKey;
            hmac.TransformBlock(ciphertext.Array, ciphertext.Offset + CONTEXT_TWEAK_LENGTH, AES_IV_LENGTH + cipherLength, null, 0);
            hmac.TransformFinalBlock(ciphertext.Array, 0, 0);
            var fullmacActual = hmac.HashInner;
            if (!Utils.ConstantTimeEqual(fullmacActual, 0, ciphertext.Array, ciphertext.Offset + ciphertext.Count - MAC_LENGTH, MAC_LENGTH)) { outputSegment = null; return; };
        }// using hmac

        Utils.BlockCopy(ciphertext.Array, ciphertext.Offset + CONTEXT_TWEAK_LENGTH, iv, 0, AES_IV_LENGTH);
        Utils.BlockCopy(sessionKey, MAC_KEY_LENGTH, encKey, 0, ENC_KEY_LENGTH);

        using (var aes = _aesFactory())
        {
            EtM_CBC.ValidateAes(aes);
            using (var aesDecryptor = aes.CreateDecryptor(encKey, iv))
            {
                int fullBlockTransformed = 0;
                if (fullBlockLength > 0)
                    fullBlockTransformed = aesDecryptor.TransformBlock(inputBuffer: ciphertext.Array, inputOffset: ciphertext.Offset + CONTEXT_BUFFER_LENGTH, inputCount: fullBlockLength, outputBuffer: outputSegment.Value.Array, outputOffset: outputSegment.Value.Offset);

                finalBlock = aesDecryptor.TransformFinalBlock(ciphertext.Array, ciphertext.Offset + CONTEXT_BUFFER_LENGTH + fullBlockLength, cipherLength - fullBlockLength);
                Utils.BlockCopy(finalBlock, 0, outputSegment.Value.Array, outputSegment.Value.Offset + fullBlockTransformed, finalBlock.Length);
                outputSegment = new ArraySegment<byte>?(new ArraySegment<byte>(outputSegment.Value.Array, outputSegment.Value.Offset, fullBlockTransformed + finalBlock.Length));
            }// using aesDecryptor
        }// using aes
    }
    finally
    {
        EtM_CBC.ClearKeyMaterial();
        if (finalBlock != null) Array.Clear(finalBlock, 0, finalBlock.Length);
    }
}// Decrypt()

public static byte[] Decrypt(byte[] masterKey, ArraySegment<byte> ciphertext, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int cipherLength = ciphertext.Count - CONTEXT_BUFFER_LENGTH - MAC_LENGTH;
    if (cipherLength < Cipher.AesConstants.AES_BLOCK_SIZE) return null;
    try
    {
        var iv = _iv.Value;
        var encKey = _encKey.Value;
        var macKey = _macKey.Value;
        var sessionKey = _sessionKey.Value;

        Kdf.SP800_108_Ctr.DeriveKey(hmacFactory: _hmacFactory, key: masterKey, label: salt, context: new ArraySegment<byte>(ciphertext.Array, ciphertext.Offset, CONTEXT_BUFFER_LENGTH), derivedOutput: sessionKey.AsArraySegment(), counter: counter);
        Utils.BlockCopy(sessionKey, 0, macKey, 0, MAC_KEY_LENGTH);

        using (var hmac = _hmacFactory())
        {
            hmac.Key = macKey;
            hmac.TransformBlock(ciphertext.Array, ciphertext.Offset + CONTEXT_TWEAK_LENGTH, AES_IV_LENGTH + cipherLength, null, 0);
            hmac.TransformFinalBlock(ciphertext.Array, 0, 0);
            var fullmacActual = hmac.HashInner;
            if (!Utils.ConstantTimeEqual(fullmacActual, 0, ciphertext.Array, ciphertext.Offset + ciphertext.Count - MAC_LENGTH, MAC_LENGTH)) return null;
        }// using hmac

        Utils.BlockCopy(ciphertext.Array, ciphertext.Offset + CONTEXT_TWEAK_LENGTH, iv, 0, AES_IV_LENGTH);
        Utils.BlockCopy(sessionKey, MAC_KEY_LENGTH, encKey, 0, ENC_KEY_LENGTH);

        using (var aes = _aesFactory())
        {
            EtM_CBC.ValidateAes(aes);
            using (var aesDecryptor = aes.CreateDecryptor(encKey, iv))
            {
                return aesDecryptor.TransformFinalBlock(ciphertext.Array, ciphertext.Offset + CONTEXT_BUFFER_LENGTH, cipherLength);
            }// using aesDecryptor
        }// using aes
    }
    finally { EtM_CBC.ClearKeyMaterial(); }
}// Decrypt()

public static bool Authenticate(byte[] masterKey, ArraySegment<byte> ciphertext, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int cipherLength = ciphertext.Count - CONTEXT_BUFFER_LENGTH - MAC_LENGTH;
    if (cipherLength < Cipher.AesConstants.AES_BLOCK_SIZE) return false;
    try
    {
        var macKey = _macKey.Value;
        var sessionKey = _sessionKey.Value;

        Kdf.SP800_108_Ctr.DeriveKey(hmacFactory: _hmacFactory, key: masterKey, label: salt, context: new ArraySegment<byte>(ciphertext.Array, ciphertext.Offset, CONTEXT_BUFFER_LENGTH), derivedOutput: sessionKey.AsArraySegment(), counter: counter);
        Utils.BlockCopy(sessionKey, 0, macKey, 0, MAC_KEY_LENGTH);
        using (var hmac = _hmacFactory())
        {
            hmac.Key = macKey;
            hmac.TransformBlock(ciphertext.Array, ciphertext.Offset + CONTEXT_TWEAK_LENGTH, AES_IV_LENGTH + cipherLength, null, 0);
            hmac.TransformFinalBlock(ciphertext.Array, 0, 0);
            var fullmacActual = hmac.HashInner;
            if (!Utils.ConstantTimeEqual(fullmacActual, 0, ciphertext.Array, ciphertext.Offset + ciphertext.Count - MAC_LENGTH, MAC_LENGTH)) return false;
        }// using hmac
        return true;
    }
    finally { EtM_CBC.ClearKeyMaterial(); }
}// Authenticate()

public static void Encrypt(byte[] masterKey, ArraySegment<byte> plaintext, byte[] output, int outputOffset, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int ciphertextLength = CONTEXT_BUFFER_LENGTH + MAC_LENGTH + plaintext.Count;
    if (output.Length - outputOffset < ciphertextLength) throw new ArgumentOutOfRangeException(nameof(output), $"'{nameof(output)}' array segment is not big enough for the ciphertext");

    var counterBuffer = new byte[Cipher.AesConstants.AES_BLOCK_SIZE];
    var contextBuffer = new byte[CONTEXT_BUFFER_LENGTH];
    var encKey = new byte[ENC_KEY_LENGTH];
    var macKey = new byte[MAC_KEY_LENGTH];
    var sessionKey = new byte[HMAC_LENGTH];

    try
    {
        _cryptoRandom.NextBytes(contextBuffer, 0, CONTEXT_BUFFER_LENGTH);

        Kdf.SP800_108_Ctr.DeriveKey(hmacFactory: _hmacFactory, key: masterKey, label: salt, context: new ArraySegment<byte>(contextBuffer, 0, CONTEXT_TWEAK_LENGTH), derivedOutput: sessionKey.AsArraySegment(), counter: counter);

        //Utils.BlockCopy(sessionKey, 0, macKey, 0, MAC_KEY_LENGTH);
        for (int i = 0; i < macKey.Length; ++i) macKey[i] = sessionKey[i];

        //Utils.BlockCopy(sessionKey, MAC_KEY_LENGTH, encKey, 0, ENC_KEY_LENGTH);
        for (int i = 0; i < encKey.Length; ++i) encKey[i] = sessionKey[MAC_KEY_LENGTH + i];

        //Utils.BlockCopy(contextBuffer, 0, output, outputOffset, CONTEXT_BUFFER_LENGTH);
        for (int i = 0; i < contextBuffer.Length; ++i) output[outputOffset + i] = contextBuffer[i];

        //Utils.BlockCopy(contextBuffer, CONTEXT_TWEAK_LENGTH, counterBuffer, 0, NONCE_LENGTH);
        for (int i = 0; i < NONCE_LENGTH; ++i) counterBuffer[i] = contextBuffer[CONTEXT_TWEAK_LENGTH + i];

        using (var ctrTransform = new Cipher.AesCtrCryptoTransform(key: encKey, counterBufferSegment: counterBuffer.AsArraySegment(), aesFactory: _aesFactory))
        {
            ctrTransform.TransformBlock(inputBuffer: plaintext.Array, inputOffset: plaintext.Offset, inputCount: plaintext.Count, outputBuffer: output, outputOffset: outputOffset + CONTEXT_BUFFER_LENGTH);
        }// using aesEncryptor

        using (var hmac = _hmacFactory())
        {
            hmac.Key = macKey;
            hmac.TransformBlock(output, outputOffset + CONTEXT_TWEAK_LENGTH, NONCE_LENGTH + plaintext.Count, null, 0);
            hmac.TransformFinalBlock(output, 0, 0);
            var fullmac = hmac.HashInner;

            //Utils.BlockCopy(fullmac, 0, output, outputOffset + ciphertextLength - MAC_LENGTH, MAC_LENGTH);
            for (int i = 0; i < MAC_LENGTH; ++i) output[outputOffset + ciphertextLength - MAC_LENGTH + i] = fullmac[i];
        }// using hmac
    }
    finally { EtM_CTR.ClearKeyMaterial(encKey, macKey, sessionKey); }
}// Encrypt()

public static byte[] Encrypt(byte[] masterKey, ArraySegment<byte> plaintext, ArraySegment<byte>? salt = null, uint counter = 1)
{
    byte[] buffer = new byte[CONTEXT_BUFFER_LENGTH + plaintext.Count + MAC_LENGTH];
    EtM_CTR.Encrypt(masterKey: masterKey, plaintext: plaintext, output: buffer, outputOffset: 0, salt: salt, counter: counter);
    return buffer;
}// Encrypt()

public static void Decrypt(byte[] masterKey, ArraySegment<byte> ciphertext, ref ArraySegment<byte>? outputSegment, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int cipherLength = ciphertext.Count - CONTEXT_BUFFER_LENGTH - MAC_LENGTH;
    if (cipherLength < 0) { outputSegment = null; return; }

    var counterBuffer = new byte[Cipher.AesConstants.AES_BLOCK_SIZE];
    var encKey = new byte[ENC_KEY_LENGTH];
    var macKey = new byte[MAC_KEY_LENGTH];
    var sessionKey = new byte[HMAC_LENGTH];

    try
    {
        var ciphertextArray = ciphertext.Array;
        var ciphertextOffset = ciphertext.Offset;

        Kdf.SP800_108_Ctr.DeriveKey(hmacFactory: _hmacFactory, key: masterKey, label: salt, context: new ArraySegment<byte>(ciphertextArray, ciphertextOffset, CONTEXT_TWEAK_LENGTH), derivedOutput: sessionKey.AsArraySegment(), counter: counter);

        //Utils.BlockCopy(sessionKey, 0, macKey, 0, MAC_KEY_LENGTH);
        for (int i = 0; i < macKey.Length; ++i) macKey[i] = sessionKey[i];

        using (var hmac = _hmacFactory())
        {
            hmac.Key = macKey;
            hmac.TransformBlock(ciphertextArray, ciphertextOffset + CONTEXT_TWEAK_LENGTH, NONCE_LENGTH + cipherLength, null, 0);
            hmac.TransformFinalBlock(ciphertextArray, 0, 0);
            var fullmacActual = hmac.HashInner;
            if (!Utils.ConstantTimeEqual(fullmacActual, 0, ciphertextArray, ciphertextOffset + ciphertext.Count - MAC_LENGTH, MAC_LENGTH)) { outputSegment = null; return; };
        }// using hmac

        if (outputSegment == null) outputSegment = (new byte[cipherLength]).AsNullableArraySegment();

        //Utils.BlockCopy(ciphertext.Array, ciphertext.Offset + CONTEXT_TWEAK_LENGTH, counterBuffer, 0, NONCE_LENGTH);
        for (int i = 0; i < NONCE_LENGTH; ++i) counterBuffer[i] = ciphertextArray[ciphertextOffset + CONTEXT_TWEAK_LENGTH + i];

        //Utils.BlockCopy(sessionKey, MAC_KEY_LENGTH, encKey, 0, ENC_KEY_LENGTH);
        for (int i = 0; i < encKey.Length; ++i) encKey[i] = sessionKey[MAC_KEY_LENGTH + i];

        using (var ctrTransform = new Cipher.AesCtrCryptoTransform(key: encKey, counterBufferSegment: counterBuffer.AsArraySegment(), aesFactory: _aesFactory))
        {
            ctrTransform.TransformBlock(inputBuffer: ciphertextArray, inputOffset: ciphertextOffset + CONTEXT_BUFFER_LENGTH, inputCount: cipherLength, outputBuffer: outputSegment.GetValueOrDefault().Array, outputOffset: outputSegment.GetValueOrDefault().Offset);
        }// using aesDecryptor
    }
    finally { EtM_CTR.ClearKeyMaterial(encKey, macKey, sessionKey); }
}// Decrypt()

public static byte[] Decrypt(byte[] masterKey, ArraySegment<byte> ciphertext, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int cipherLength = ciphertext.Count - CONTEXT_BUFFER_LENGTH - MAC_LENGTH;
    if (cipherLength < 0) return null;
    var bufferSegment = default(ArraySegment<byte>?);
    EtM_CTR.Decrypt(masterKey, ciphertext, ref bufferSegment, salt, counter);
    return (bufferSegment != null) ? bufferSegment.GetValueOrDefault().Array : null;
}// Decrypt()

public static bool Authenticate(byte[] masterKey, ArraySegment<byte> ciphertext, ArraySegment<byte>? salt = null, uint counter = 1)
{
    int cipherLength = ciphertext.Count - CONTEXT_BUFFER_LENGTH - MAC_LENGTH;
    if (cipherLength < 0) return false;

    var macKey = new byte[MAC_KEY_LENGTH];
    var sessionKey = new byte[HMAC_LENGTH];

    try
    {
        var ciphertextArray = ciphertext.Array;
        var ciphertextOffset = ciphertext.Offset;

        Kdf.SP800_108_Ctr.DeriveKey(hmacFactory: _hmacFactory, key: masterKey, label: salt, context: new ArraySegment<byte>(ciphertextArray, ciphertextOffset, CONTEXT_TWEAK_LENGTH), derivedOutput: sessionKey.AsArraySegment(), counter: counter);

        //Utils.BlockCopy(sessionKey, 0, macKey, 0, MAC_KEY_LENGTH);
        for (int i = 0; i < macKey.Length; ++i) macKey[i] = sessionKey[i];

        using (var hmac = _hmacFactory())
        {
            hmac.Key = macKey;
            hmac.TransformBlock(ciphertextArray, ciphertextOffset + CONTEXT_TWEAK_LENGTH, NONCE_LENGTH + cipherLength, null, 0);
            hmac.TransformFinalBlock(ciphertextArray, 0, 0);
            var fullmacActual = hmac.HashInner;
            if (!Utils.ConstantTimeEqual(fullmacActual, 0, ciphertextArray, ciphertextOffset + ciphertext.Count - MAC_LENGTH, MAC_LENGTH)) return false;
        }// using hmac
        return true;
    }
    finally { EtM_CTR.ClearKeyMaterial(encKey: Array.Empty<byte>(), macKey: macKey, sessionKey: sessionKey); }
}// Authenticate()

public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
{
    int partialBlockSize = inputCount % EtM_Transform_Constants.INPUT_BLOCK_SIZE;
    int fullBlockSize = inputCount - partialBlockSize;

    if (partialBlockSize != 0)
        throw new Exception("inputCount must be a multiple of input block size (" + EtM_Transform_Constants.INPUT_BLOCK_SIZE.ToString() + ").");

    int i = 0, j = 0;
    if (fullBlockSize > 0)
    {
        for (; i < fullBlockSize; i += EtM_Transform_Constants.INPUT_BLOCK_SIZE, j += EtM_Transform_Constants.OUTPUT_BLOCK_SIZE)
        {
            EtM_CTR.Encrypt(
                masterKey: this.key,
                plaintext: new ArraySegment<byte>(inputBuffer, inputOffset + i, EtM_Transform_Constants.INPUT_BLOCK_SIZE),
                output: outputBuffer,
                outputOffset: outputOffset + j,
                salt: this.salt,
                counter: this.currentChunkNumber);

            if (this.currentChunkNumber == EtM_Transform_Constants.INITIAL_CHUNK_NUMBER)
            {
                EtM_EncryptTransform.PrependSaltWith1stBlockContext(ref this.salt, outputBuffer, outputOffset);
            }

            checked { ++this.currentChunkNumber; }
        }
    }
    return j;
}// TransformBlock()

public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
{
    if (this.key == null) return null; // key would be null if this instance has already been disposed
    if (inputCount >= EtM_Transform_Constants.INPUT_BLOCK_SIZE)
        throw new Exception("Final input block size must be smaller than " + EtM_Transform_Constants.INPUT_BLOCK_SIZE.ToString() + ".");

    byte[] outputBuffer = new byte[EtM_Transform_Constants.ETM_CTR_OVERHEAD + inputCount];

    EtM_CTR.Encrypt(
        masterKey: this.key,
        plaintext: new ArraySegment<byte>(inputBuffer, inputOffset, inputCount),
        output: outputBuffer,
        outputOffset: 0,
        salt: this.salt,
        counter: this.currentChunkNumber);

    this.Dispose();
    return outputBuffer;
}// TransformFinalBlock()

public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
{
    int partialBlockSize = inputCount % EtM_Transform_Constants.OUTPUT_BLOCK_SIZE;
    int fullBlockSize = inputCount - partialBlockSize;

    if (partialBlockSize != 0)
        throw new Exception("inputCount must be a multiple of output block size (" + EtM_Transform_Constants.OUTPUT_BLOCK_SIZE.ToString() + ").");

    int i = 0, j = 0;
    if (fullBlockSize > 0)
    {
        var authenticateOnly = this.IsAuthenticateOnly;
        for (; i < fullBlockSize; i += EtM_Transform_Constants.OUTPUT_BLOCK_SIZE, j += EtM_Transform_Constants.INPUT_BLOCK_SIZE)
        {
            var outputSegment = new ArraySegment<byte>?(new ArraySegment<byte>(outputBuffer, outputOffset + j, EtM_Transform_Constants.INPUT_BLOCK_SIZE));
            var cipherText = new ArraySegment<byte>(inputBuffer, inputOffset + i, EtM_Transform_Constants.OUTPUT_BLOCK_SIZE);

            if (authenticateOnly)
            {
                if (!EtM_CTR.Authenticate(
                    masterKey: this.key,
                    ciphertext: cipherText,
                    salt: this.salt,
                    counter: this.currentChunkNumber))
                    outputSegment = null;
            }
            else
            {
                EtM_CTR.Decrypt(
                    masterKey: this.key,
                    ciphertext: cipherText,
                    outputSegment: ref outputSegment,
                    salt: this.salt,
                    counter: this.currentChunkNumber);
            }

            if (outputSegment == null)
            {
                this.key = null;
                throw new CryptographicException("Decryption failed for block " + this.currentChunkNumber.ToString() + ".");
            }

            if (this.currentChunkNumber == EtM_Transform_Constants.INITIAL_CHUNK_NUMBER)
            {
                EtM_EncryptTransform.PrependSaltWith1stBlockContext(ref this.salt, inputBuffer, inputOffset);
            }

            checked { ++this.currentChunkNumber; }
        }
    }
    return j;
}// TransformBlock()

public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
{
    if (this.key == null) return null; // key would be null if this instance has already been disposed, or previously-called TransformBlock() failed
    if (inputCount >= EtM_Transform_Constants.OUTPUT_BLOCK_SIZE)
        throw new Exception("Final output block size must be smaller than " + EtM_Transform_Constants.OUTPUT_BLOCK_SIZE.ToString() + ".");

    if (inputCount < EtM_Transform_Constants.ETM_CTR_OVERHEAD)
        throw new Exception("Final output block size must must be at least " + EtM_Transform_Constants.ETM_CTR_OVERHEAD.ToString() + ".");

    byte[] outputBuffer = null;
    var cipherText = new ArraySegment<byte>(inputBuffer, inputOffset, inputCount);

    if (this.IsAuthenticateOnly)
    {
        if (EtM_CTR.Authenticate(
            masterKey: this.key,
            ciphertext: cipherText,
            salt: this.salt,
            counter: this.currentChunkNumber))
            outputBuffer = Array.Empty<byte>();
    }
    else
    {
        outputBuffer = EtM_CTR.Decrypt(
            masterKey: this.key,
            ciphertext: cipherText,
            salt: this.salt,
            counter: this.currentChunkNumber);
    }
    this.Dispose();
    if (outputBuffer == null)
        throw new CryptographicException("Decryption failed for block " + this.currentChunkNumber.ToString() + ".");

    this.IsComplete = true;
    return outputBuffer;
}// TransformFinalBlock()

