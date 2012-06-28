#pragma once
//-------------------------------------------------------------------------------------------------
// <copyright file="dictutil.h" company="Microsoft">
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
//    Header for string dict helper functions.
// </summary>
//-------------------------------------------------------------------------------------------------

#ifdef __cplusplus
extern "C" {
#endif

#define ReleaseDict(sdh) if (sdh) { DictDestroy(sdh); }
#define ReleaseNullDict(sdh) if (sdh) { DictDestroy(sdh); sdh = NULL; }

typedef void* STRINGDICT_HANDLE;
typedef const void* C_STRINGDICT_HANDLE;

extern const int STRINGDICT_HANDLE_BYTES;

enum DICT_FLAG
{
    DICT_FLAG_NONE = 0,
    DICT_FLAG_CASEINSENSITIVE = 1
};

HRESULT DAPI DictCreateWithEmbeddedKey(
    __out_bcount(STRINGDICT_HANDLE_BYTES) STRINGDICT_HANDLE* psdHandle,
    __in DWORD dwNumExpectedItems,
    __in_opt void **ppvArray,
    __in size_t cByteOffset,
    __in DICT_FLAG dfFlags
    );
HRESULT DAPI DictCreateStringList(
    __out_bcount(STRINGDICT_HANDLE_BYTES) STRINGDICT_HANDLE* psdHandle,
    __in DWORD dwNumExpectedItems,
    __in DICT_FLAG dfFlags
    );
HRESULT DAPI DictAddKey(
    __in_bcount(STRINGDICT_HANDLE_BYTES) STRINGDICT_HANDLE sdHandle,
    __in_z LPCWSTR szString
    );
HRESULT DAPI DictAddValue(
    __in_bcount(STRINGDICT_HANDLE_BYTES) STRINGDICT_HANDLE sdHandle,
    __in void *pvValue
    );
HRESULT DAPI DictKeyExists(
    __in_bcount(STRINGDICT_HANDLE_BYTES) C_STRINGDICT_HANDLE sdHandle,
    __in_z LPCWSTR szString
    );
HRESULT DAPI DictGetValue(
    __in_bcount(STRINGDICT_HANDLE_BYTES) C_STRINGDICT_HANDLE sdHandle,
    __in_z LPCWSTR szString,
    __out void **ppvValue
    );
void DAPI DictDestroy(
    __in_bcount(STRINGDICT_HANDLE_BYTES) STRINGDICT_HANDLE sdHandle
    );

#ifdef __cplusplus
}
#endif
