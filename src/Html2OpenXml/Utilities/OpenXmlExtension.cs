﻿/* Copyright (C) Olivier Nizet https://github.com/onizet/html2openxml - All Rights Reserved
 * 
 * This source is subject to the Microsoft Permissive License.
 * Please see the License.txt file for more information.
 * All other rights reserved.
 * 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 */
using System;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml
{
    using wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

    /// <summary>
    /// Helper class that provide some extension methods to OpenXml SDK.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    static class OpenXmlExtension
    {
        public static bool HasChild<T>(this OpenXmlElement element) where T : OpenXmlElement
        {
            return element.GetFirstChild<T>() != null;
        }

        public static T? GetLastChild<T>(this OpenXmlElement element) where T : OpenXmlElement
        {
            if (element == null) return null;

            for (int i = element.ChildElements.Count - 1; i >= 0; i--)
            {
                if (element.ChildElements[i] is T)
                    return element.ChildElements[i] as T;
            }

            return null;
        }

        public static bool Equals<T>(this IEnumValue value, T comparand) where T : struct
        {
            return value != null && value.Value.Equals(comparand);
        }

        public static void InsertInProperties(this Paragraph p, Action<ParagraphProperties> @delegate)
        {
            p.ParagraphProperties ??= new ParagraphProperties();
            @delegate(p.ParagraphProperties);
        }

        public static void InsertInProperties(this Run r, Action<RunProperties> @delegate)
        {
            r.RunProperties ??= new RunProperties();
            @delegate(r.RunProperties);
        }

        public static void InsertInDocProperties(this Drawing d, params OpenXmlElement[] newChildren)
        {
            d.Inline ??= new wp.Inline();
            if (d.Inline.DocProperties == null) d.Inline.DocProperties = new wp.DocProperties();

            d.Inline.DocProperties.Append(newChildren);
        }

        public static bool Compare(this PageSize pageSize, PageOrientationValues orientation)
        {
            PageOrientationValues pageOrientation;

            if (pageSize.Orient != null) pageOrientation = pageSize.Orient.Value;
            else if (pageSize.Width > pageSize.Height) pageOrientation = PageOrientationValues.Landscape;
            else pageOrientation = PageOrientationValues.Portrait;

            return pageOrientation == orientation;
        }

        // needed since December 2009 CTP refactoring, where casting is not anymore an option

        public static TableRowAlignmentValues ToTableRowAlignment(this JustificationValues val)
        {
            if (val == JustificationValues.Center) return TableRowAlignmentValues.Center;
            else if (val == JustificationValues.Right) return TableRowAlignmentValues.Right;
            else return TableRowAlignmentValues.Left;
        }
        public static JustificationValues ToJustification(this TableRowAlignmentValues val)
        {
            if (val == TableRowAlignmentValues.Left) return JustificationValues.Left;
            else if (val == TableRowAlignmentValues.Center) return JustificationValues.Center;
            else return JustificationValues.Right;
        }
    }
}