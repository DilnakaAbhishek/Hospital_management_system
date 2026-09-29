using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HospitalManagementSystem.Api.AgenticAI.MedicalReports;

/// <summary>
/// Provides deterministic heuristics to classify whether an uploaded file
/// is plausibly a clinical document, or is likely a non-medical attachment.
/// This is a lightweight first-pass check — it does NOT perform deep content
/// analysis. The Gemini agent performs a second-pass classification at query time.
/// </summary>
public static class MedicalDocumentValidator
{
    // ── Allowed MIME types for medical documents ─────────────────────────────
    private static readonly HashSet<string> AcceptedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/tiff",
        "image/webp",
        "image/dicom",
        "application/dicom",
        "text/plain",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // .docx
        "application/msword",                                                      // .doc
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",       // .xlsx (lab data)
    };

    // ── Filename patterns that are clearly NOT medical ───────────────────────
    // Matched case-insensitively against the base filename (no path).
    private static readonly Regex NonMedicalFilenamePattern = new(
        @"\b(invoice|receipt|ticket|boarding[\s_-]?pass|itinerary|travel|passport|visa|" +
        @"resume|cv|curriculum[\s_-]?vitae|cover[\s_-]?letter|bank[\s_-]?statement|" +
        @"salary|payslip|tax|utility[\s_-]?bill|electricity|water[\s_-]?bill|" +
        @"insurance[\s_-]?claim|product[\s_-]?manual|warranty|user[\s_-]?guide|" +
        @"screenshot|selfie|photo|wallpaper|meme|artwork|poster|brochure|flyer|" +
        @"invoice|purchase[\s_-]?order|delivery[\s_-]?note|packing[\s_-]?list)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Filename patterns that suggest medical content ────────────────────────
    private static readonly Regex MedicalFilenamePattern = new(
        @"\b(report|lab|blood|urine|scan|mri|ct|xray|x[\s_-]?ray|ecg|ekg|ultrasound|" +
        @"pathology|biopsy|prescription|rx|medication|dosage|discharge|diagnosis|" +
        @"clinical|medical|patient|consultation|referral|radiology|haematology|" +
        @"haemoglobin|glucose|cholesterol|culture|sensitivity|covid|pcr|antibody|" +
        @"vaccine|vaccination|immunization|cardiology|oncology|gynaecology|" +
        @"neurology|orthopaedic|ophthalmology|dermatology|pharmacy|hospital|clinic)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Classifies an uploaded file as Medical, Ambiguous, or NonMedical.
    /// Returns the classification and a human-readable reason.
    /// </summary>
    public static (DocumentCategory Category, string Reason) Classify(
        string fileName, string contentType)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        var normalized = Regex.Replace(baseName, @"[_\-\.]+", " ");
        var ext = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
        var mime = (contentType ?? string.Empty).Split(';')[0].Trim();

        // 1. Hard-reject MIME types that are never medical documents
        if (!AcceptedMimeTypes.Contains(mime) && !string.IsNullOrWhiteSpace(mime) && mime != "application/octet-stream")
        {
            return (DocumentCategory.NonMedical,
                $"File type '{mime}' is not accepted for medical records. " +
                "Please upload a PDF, image (JPG, PNG, TIFF), or Word document.");
        }

        // 2. Filename looks explicitly non-medical
        if (NonMedicalFilenamePattern.IsMatch(normalized) || NonMedicalFilenamePattern.IsMatch(baseName))
        {
            return (DocumentCategory.NonMedical,
                $"The filename '{fileName}' does not appear to be a medical document. " +
                "Please ensure you are uploading a relevant clinical document such as a " +
                "lab report, scan, prescription, or discharge summary.");
        }

        // 3. Filename contains medical keywords — high confidence
        if (MedicalFilenamePattern.IsMatch(normalized) || MedicalFilenamePattern.IsMatch(baseName))
            return (DocumentCategory.Medical, "Filename contains recognized clinical terminology.");

        // 4. PDF with no medical keywords — ambiguous (patient may rename files)
        if (ext == "pdf" || mime == "application/pdf")
            return (DocumentCategory.Ambiguous,
                "PDF uploaded. Could not confirm from the filename whether this is a medical document. " +
                "The AI assistant will evaluate the content of this record at query time.");

        // 5. Image with no medical keywords — ambiguous (could be a scan/photo)
        if (ext is "jpg" or "jpeg" or "png" or "tiff" or "webp")
            return (DocumentCategory.Ambiguous,
                "Image uploaded. Could not confirm from the filename whether this is a medical scan or image. " +
                "The AI assistant will evaluate at query time.");

        // 6. Default — ambiguous
        return (DocumentCategory.Ambiguous,
            "File uploaded. Relevance to medical records could not be determined from the filename alone.");
    }

    /// <summary>
    /// Returns true if the filename looks like a medical document (or is ambiguous).
    /// Returns false only for clearly non-medical files.
    /// </summary>
    public static bool IsLikelyMedical(string fileName, string contentType)
        => Classify(fileName, contentType).Category != DocumentCategory.NonMedical;

    /// <summary>
    /// Checks whether typed clinical text appears to be uninformative keyboard mash,
    /// random placeholder text (e.g. "asdfghjkl", "qwerty", "test123", "none", etc.), or nonsensical characters.
    /// </summary>
    public static bool IsGarbageOrPlaceholder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        var trimmed = text.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("n/a", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("nil", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("--", StringComparison.OrdinalIgnoreCase))
            return true;

        // Common keyboard mash or dummy prefixes
        if (Regex.IsMatch(trimmed, @"^(asdf|qwerty|zxcv|1234|test|dummy|placeholder|xyz123|foobar|blah)", RegexOptions.IgnoreCase))
            return true;

        // Long single-word strings without vowels or mostly consonants (e.g. "ghjklnm", "asdfghjkl")
        if (trimmed.Length >= 6 && !trimmed.Contains(' ') && Regex.IsMatch(trimmed, @"^[bcdfghjklmnpqrstvwxyz0-9_]+$", RegexOptions.IgnoreCase))
            return true;

        // Repetitive single character (e.g. "aaaaa", "11111", "---")
        if (trimmed.Distinct().Count() <= 2 && trimmed.Length >= 4)
            return true;

        return false;
    }
}

public enum DocumentCategory
{
    /// <summary>Filename / MIME clearly matches known medical document patterns.</summary>
    Medical,

    /// <summary>Cannot determine from metadata alone — needs AI or content inspection.</summary>
    Ambiguous,

    /// <summary>Filename / MIME clearly does not match a medical document.</summary>
    NonMedical
}
