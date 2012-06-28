#pragma once
//-------------------------------------------------------------------------------------------------
// <copyright file="deputil.h" company="Microsoft">
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
//    Common function declarations for the dependency/ref-counting feature.
// </summary>
//-------------------------------------------------------------------------------------------------

#ifdef __cplusplus
extern "C" {
#endif

#define ReleaseDependencyArray(rg, c) if (rg) { DepDependencyArrayFree(rg, c); }
#define ReleaseNullDependencyArray(rg, c) if (rg) { DepDependencyArrayFree(rg, c); rg = NULL; }

typedef struct _DEPENDENCY
{
    LPWSTR sczKey;
    LPWSTR sczName;

} DEPENDENCY;


/***************************************************************************
 DepCheckDependency - Checks that the dependency is registered and within
                      the proper version range.

 Note: Returns E_NOTFOUND if the dependency was not found.
***************************************************************************/
DAPI_(HRESULT) DepCheckDependency(
    __in HKEY hkHive,
    __in_z LPCWSTR wzProviderKey,
    __in_z_opt LPCWSTR wzMinVersion,
    __in_z_opt LPCWSTR wzMaxVersion,
    __in int iAttributes,
    __in STRINGDICT_HANDLE sdDependencies,
    __deref_inout_ecount_opt(*pcDependencies) DEPENDENCY** prgDependencies,
    __inout LPUINT pcDependencies
    );

/***************************************************************************
 DepCheckDependents - Checks if any dependents are still installed for the
                      given provider key.

***************************************************************************/
DAPI_(HRESULT) DepCheckDependents(
    __in HKEY hkHive,
    __in_z LPCWSTR wzProviderKey,
    __in int iAttributes,
    __in C_STRINGDICT_HANDLE sdIgnoredDependents,
    __deref_inout_ecount_opt(*pcDependents) DEPENDENCY** prgDependents,
    __inout LPUINT pcDependents
    );

/***************************************************************************
 DepRegisterDependency - Registers the dependency provider.

***************************************************************************/
DAPI_(HRESULT) DepRegisterDependency(
    __in HKEY hkHive,
    __in_z LPCWSTR wzProviderKey,
    __in_z LPCWSTR wzVersion,
    __in_z LPCWSTR wzDisplayName,
    __in int iAttributes
    );

/***************************************************************************
 DepRegisterDependent - Registers a dependent under the dependency provider.

***************************************************************************/
DAPI_(HRESULT) DepRegisterDependent(
    __in HKEY hkHive,
    __in_z LPCWSTR wzDependencyProviderKey,
    __in_z LPCWSTR wzProviderKey,
    __in_z_opt LPCWSTR wzMinVersion,
    __in_z_opt LPCWSTR wzMaxVersion,
    __in int iAttributes
    );

/***************************************************************************
 DepUnregisterDependency - Removes the dependency provider.

 Note: Caller should call CheckDependents prior to remove a dependency.
       Returns E_FILENOTFOUND if the dependency is not registered.
***************************************************************************/
DAPI_(HRESULT) DepUnregisterDependency(
    __in HKEY hkHive,
    __in_z LPCWSTR wzProviderKey
    );

/***************************************************************************
 DepUnregisterDependent - Removes a dependent under the dependency provider.

 Note: Returns E_FILENOTFOUND if neither the dependency or dependent are
       registered.
 ***************************************************************************/
DAPI_(HRESULT) DepUnregisterDependent(
    __in HKEY hkHive,
    __in_z LPCWSTR wzDependencyProviderKey,
    __in_z LPCWSTR wzProviderKey
    );

/***************************************************************************
 DependencyArrayAlloc - Allocates or expands an array of DEPENDENCY structs.

***************************************************************************/
DAPI_(HRESULT) DepDependencyArrayAlloc(
    __deref_inout_ecount_opt(*pcDependencies) DEPENDENCY** prgDependencies,
    __inout LPUINT pcDependencies,
    __in_z LPCWSTR wzKey,
    __in_z_opt LPCWSTR wzName
    );

/***************************************************************************
 DepDependencyArrayFree - Frees an array of DEPENDENCY structs.

***************************************************************************/
DAPI_(void) DepDependencyArrayFree(
    __in_ecount(cDependencies) DEPENDENCY* rgDependencies,
    __in UINT cDependencies
    );

#ifdef __cplusplus
}
#endif
