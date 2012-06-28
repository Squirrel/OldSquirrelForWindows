#pragma once
//-------------------------------------------------------------------------------------------------
// <copyright file="balcondition.h" company="Microsoft">
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
// Bootstrapper Application Layer condition utility.
// </summary>
//-------------------------------------------------------------------------------------------------


#ifdef __cplusplus
extern "C" {
#endif

typedef struct _BAL_CONDITION
{
    LPWSTR sczCondition;
    LPWSTR sczMessage;
} BAL_CONDITION;


typedef struct _BAL_CONDITIONS
{
    BAL_CONDITION* rgConditions;
    DWORD cConditions;
} BAL_CONDITIONS;


/*******************************************************************
 BalConditionsParseFromXml - loads the conditions from the UX manifest.

********************************************************************/
DAPI_(HRESULT) BalConditionsParseFromXml(
    __in BAL_CONDITIONS* pConditions,
    __in IXMLDOMDocument* pixdManifest,
    __in_opt WIX_LOCALIZATION* pWixLoc
    );


/*******************************************************************
 BalConditionEvaluate - evaluates condition against the provided IBurnCore.

 NOTE: psczMessage is optional.
********************************************************************/
DAPI_(HRESULT) BalConditionEvaluate(
    __in BAL_CONDITION* pCondition,
    __in IBootstrapperEngine* pEngine,
    __out BOOL* pfResult,
    __out_z_opt LPWSTR* psczMessage
    );


/*******************************************************************
 BalConditionsUninitialize - uninitializes any conditions previously loaded.

********************************************************************/
DAPI_(void) BalConditionsUninitialize(
    __in BAL_CONDITIONS* pConditions
    );


#ifdef __cplusplus
}
#endif
