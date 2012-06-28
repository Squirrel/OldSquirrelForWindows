#pragma once
//-------------------------------------------------------------------------------------------------
// <copyright file="cryputil.h" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
//    
//    The use and distribution terms for this software are covered by the
//    Common Public License 1.0 (http://opensource.org/licenses/cpl1.0.php)
//    which can be found in the file CPL.TXT at the root of this distribution.
//    By using this software in any fashion, you are agreeing to be bound by
//    the terms of this license.
//    
//    You must not remove this notice, or any other, from this software.
// </copyright>
//
// <summary>
//    Cryptography helper functions.
// </summary>
//-------------------------------------------------------------------------------------------------

#define ReleaseCryptMsg(p) if (p) { ::CryptMsgClose(p); p = NULL; }

#ifdef __cplusplus
extern "C" {
#endif


#define SHA1_HASH_LEN 20

// function declarations

HRESULT DAPI CrypDecodeObject(
    __in_z LPCSTR szStructType,
    __in_ecount(cbData) const BYTE* pbData,
    __in DWORD cbData,
    __in DWORD dwFlags,
    __out LPVOID* ppvObject,
    __out_opt DWORD* pcbObject
    );

HRESULT DAPI CrypMsgGetParam(
    __in HCRYPTMSG hCryptMsg,
    __in DWORD dwType,
    __in DWORD dwIndex,
    __out LPVOID* ppvData,
    __out_opt DWORD* pcbData
    );

HRESULT DAPI CrypHashFile(
    __in_z LPCWSTR wzFilePath,
    __in DWORD dwProvType,
    __in ALG_ID algid,
    __out_bcount(cbHash) BYTE* pbHash,
    __in DWORD cbHash,
    __out_opt DWORD64* pqwBytesHashed
    );

HRESULT DAPI CrypHashFileHandle(
    __in HANDLE hFile,
    __in DWORD dwProvType,
    __in ALG_ID algid,
    __out_bcount(cbHash) BYTE* pbHash,
    __in DWORD cbHash,
    __out_opt DWORD64* pqwBytesHashed
    );

HRESULT DAPI CrypHashBuffer(
    __in_bcount(cbBuffer) const BYTE* pbBuffer,
    __in SIZE_T cbBuffer,
    __in DWORD dwProvType,
    __in ALG_ID algid,
    __out_bcount(cbHash) BYTE* pbHash,
    __in DWORD cbHash
    );

#ifdef __cplusplus
}
#endif
