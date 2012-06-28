#pragma once
//-------------------------------------------------------------------------------------------------
// <copyright file="balutil.h" company="Microsoft">
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
// Burn utility library.
// </summary>
//-------------------------------------------------------------------------------------------------

#include "dutil.h"


#ifdef __cplusplus
extern "C" {
#endif

#define BalExitOnFailure(x, f) if (FAILED(x)) { BalLogError(x, f); ExitTrace(x, f); goto LExit; }
#define BalExitOnFailure1(x, f, s) if (FAILED(x)) { BalLogError(x, f, s); ExitTrace1(x, f, s); goto LExit; }
#define BalExitOnFailure2(x, f, s, t) if (FAILED(x)) { BalLogError(x, f, s, t); ExitTrace2(x, f, s, t); goto LExit; }
#define BalExitOnFailure3(x, f, s, t, u) if (FAILED(x)) { BalLogError(x, f, s, t, u); ExitTrace3(x, f, s, t, u); goto LExit; }

#define BalExitOnRootFailure(x, f) if (FAILED(x)) { BalLogError(x, f); Dutil_RootFailure(__FILE__, __LINE__, x); ExitTrace(x, f); goto LExit; }
#define BalExitOnRootFailure1(x, f, s) if (FAILED(x)) { BalLogError(x, f, s); Dutil_RootFailure(__FILE__, __LINE__, x); ExitTrace1(x, f, s); goto LExit; }
#define BalExitOnRootFailure2(x, f, s, t) if (FAILED(x)) { BalLogError(x, f, s, t); Dutil_RootFailure(__FILE__, __LINE__, x); ExitTrace2(x, f, s, t); goto LExit; }
#define BalExitOnRootFailure3(x, f, s, t, u) if (FAILED(x)) { BalLogError(x, f, s, t, u); Dutil_RootFailure(__FILE__, __LINE__, x); ExitTrace3(x, f, s, t, u); goto LExit; }

/*******************************************************************
 BalInitialize - remembers the engine interface to enable logging and
                 other functions.

********************************************************************/
DAPI_(void) BalInitialize(
    __in IBootstrapperEngine* pEngine
    );

/*******************************************************************
 BalUninitialize - cleans up utility layer internals.

********************************************************************/
DAPI_(void) BalUninitialize();

/*******************************************************************
 BalManifestLoad - loads the Application manifest into an XML document.

********************************************************************/
DAPI_(HRESULT) BalManifestLoad(
    __in HMODULE hUXModule,
    __out IXMLDOMDocument** ppixdManifest
    );

/*******************************************************************
BalFormatString - formats a string using variables in the engine.

 Note: Use StrFree() to release psczOut.
********************************************************************/
DAPI_(HRESULT) BalFormatString(
    __in_z LPCWSTR wzFormat,
    __inout LPWSTR* psczOut
    );

/*******************************************************************
BalStringVariableExists - checks if a string variable exists in the engine.

********************************************************************/
DAPI_(BOOL) BalStringVariableExists(
    __in_z LPCWSTR wzVariable
    );

/*******************************************************************
BalGetStringVariable - gets a string from a variable in the engine.

 Note: Use StrFree() to release psczValue.
********************************************************************/
DAPI_(HRESULT) BalGetStringVariable(
    __in_z LPCWSTR wzVariable,
    __inout LPWSTR* psczValue
    );

/*******************************************************************
 BalLog - logs a message with the engine.

********************************************************************/
DAPIV_(HRESULT) BalLog(
    __in BOOTSTRAPPER_LOG_LEVEL level,
    __in_z __format_string LPCSTR szFormat,
    ...
    );

/*******************************************************************
 BalLogError - logs an error message with the engine.

********************************************************************/
DAPIV_(HRESULT) BalLogError(
    __in HRESULT hr,
    __in_z __format_string LPCSTR szFormat,
    ...
    );

#ifdef __cplusplus
}
#endif
