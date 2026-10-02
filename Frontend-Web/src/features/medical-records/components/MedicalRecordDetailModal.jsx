import { useState } from "react";
import {
  Calendar,
  User,
  Stethoscope,
  FileText,
  Paperclip,
  Activity,
  Pill,
  FlaskConical,
  FileSpreadsheet,
  Download,
  CheckCircle2,
  Image as ImageIcon,
  ZoomIn,
  ZoomOut,
  RotateCw,
  RefreshCw,
  Eye,
} from "lucide-react";
import Modal from "../../../components/Modal";
import Button from "../../../components/Button";
import Badge from "../../../components/Badge";

export const getCleanFileName = (fileName) => {
  if (!fileName) return "medical_document.pdf";
  // Remove 32-hex GUID prefix (e.g. 4a883a2db80e49518002aaf6c5304caf_name.pdf)
  // or 36-char GUID with hyphens (e.g. 4a883a2d-b80e-4951-8002-aaf6c5304caf_name.pdf)
  return fileName
    .replace(/^[a-fA-F0-9]{32}_/, "")
    .replace(/^[a-fA-F0-9-]{36}_/, "");
};

export const getFullAttachmentUrl = (fileUrl) => {
  if (!fileUrl) return "";
  if (
    fileUrl.startsWith("data:") ||
    fileUrl.startsWith("blob:") ||
    fileUrl.startsWith("http://") ||
    fileUrl.startsWith("https://")
  ) {
    return fileUrl;
  }
  const defaultApi =
    typeof window !== "undefined" &&
    window.location.hostname !== "localhost" &&
    window.location.hostname !== "127.0.0.1"
      ? "https://medicore-api-eed4gnc2cnckgjf3.indiasouthcentral-01.azurewebsites.net/api"
      : "http://localhost:5000";
  const apiBase = import.meta.env.VITE_API_URL || defaultApi;
  const origin = apiBase.replace(/\/api\/?$/, "");
  const cleanPath = fileUrl.startsWith("/") ? fileUrl : `/${fileUrl}`;
  return `${origin}${cleanPath}`;
};

export default function MedicalRecordDetailModal({
  record,
  onClose,
  onEdit,
  canEdit = true,
}) {
  const [previewAttachment, setPreviewAttachment] = useState(null);
  const [zoom, setZoom] = useState(1);
  const [rotation, setRotation] = useState(0);
  const [imageLoadError, setImageLoadError] = useState(false);

  if (!record) return null;

  const getTypeVariant = (type) => {
    switch (type) {
      case "Consultation":
        return "primary";
      case "LabReport":
        return "info";
      case "DischargeSummary":
        return "warning";
      case "Prescription":
        return "accent";
      default:
        return "default";
    }
  };

  const formatFileSize = (bytes) => {
    if (!bytes) return "0 KB";
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(1)} KB`;
    return `${(kb / 1024).toFixed(1)} MB`;
  };

  const openPreview = (att) => {
    setPreviewAttachment(att);
    setZoom(1);
    setRotation(0);
    setImageLoadError(false);
  };

  const handleDownloadAttachment = async (att) => {
    if (!att) return;
    const cleanName = getCleanFileName(att.fileName);

    const cachedDataUrl =
      typeof window !== "undefined"
        ? sessionStorage.getItem(`med_preview_${att.fileName}`)
        : null;

    // 1. If in-memory or sessionStorage data URL exists, download immediately
    if (
      cachedDataUrl ||
      att.fileUrl?.startsWith("data:") ||
      att.fileUrl?.startsWith("blob:")
    ) {
      const href = cachedDataUrl || att.fileUrl;
      const a = document.createElement("a");
      a.href = href;
      a.download = cleanName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      return;
    }

    // 2. Stream through backend API endpoint (handles CORS and sets clean Content-Disposition)
    const defaultApi =
      typeof window !== "undefined" &&
      window.location.hostname !== "localhost" &&
      window.location.hostname !== "127.0.0.1"
        ? "https://medicore-api-eed4gnc2cnckgjf3.indiasouthcentral-01.azurewebsites.net/api"
        : "http://localhost:5000/api";
    const apiBase = import.meta.env.VITE_API_URL || defaultApi;
    const cleanApiBase = apiBase.endsWith("/") ? apiBase.slice(0, -1) : apiBase;
    const recordId = record?.medicalRecordId;
    const backendDownloadUrl =
      att.attachmentId && recordId
        ? `${cleanApiBase}/medicalrecord/${recordId}/attachments/${att.attachmentId}/download`
        : att.attachmentId
          ? `${cleanApiBase}/medicalrecord/attachments/${att.attachmentId}/download`
          : getFullAttachmentUrl(att.fileUrl);

    try {
      const token =
        typeof window !== "undefined" &&
        window.localStorage &&
        typeof window.localStorage.getItem === "function"
          ? (window.localStorage.getItem("hms_token") || window.localStorage.getItem("token"))
          : null;
      const response = await fetch(backendDownloadUrl, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });

      if (response.ok) {
        const blob = await response.blob();
        const blobUrl = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = blobUrl;
        a.download = cleanName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(blobUrl), 10000);
        return;
      }
    } catch (err) {
      console.warn(
        "Backend proxy download failed, attempting direct fetch:",
        err,
      );
    }

    // 3. Fallback: Direct fetch into blob
    try {
      const directUrl = getFullAttachmentUrl(att.fileUrl);
      const directResp = await fetch(directUrl);
      if (directResp.ok) {
        const blob = await directResp.blob();
        const blobUrl = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = blobUrl;
        a.download = cleanName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(blobUrl), 10000);
        return;
      }
    } catch (err) {
      console.warn("Direct blob fetch failed:", err);
    }

    // 4. Final Fallback: Direct download trigger without target="_blank"
    const fallbackUrl = getFullAttachmentUrl(att.fileUrl);
    const a = document.createElement("a");
    a.href = fallbackUrl;
    a.download = cleanName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  return (
    <>
      <Modal
        open={Boolean(record)}
        onClose={onClose}
        title={
          <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
            <FileText size={20} color="var(--clr-primary)" />
            <span>Medical Record #{record.medicalRecordId}</span>
            <Badge variant={getTypeVariant(record.recordType)}>
              {record.recordType}
            </Badge>
            <Badge
              variant={record.status === "Finalized" ? "success" : "default"}
              dot
            >
              {record.status}
            </Badge>
          </div>
        }
        subtitle={`Patient: ${record.patientName} (${record.patientEmail})`}
        size="lg"
        id="medical-record-detail-modal"
      >
        <div className="mr-detail">
          {/* Top Info Banner */}
          <div className="mr-detail__grid">
            <div className="mr-detail__card">
              <div className="mr-detail__card-label">
                <User size={14} /> Patient
              </div>
              <div className="mr-detail__card-val font-semibold">
                {record.patientName || "Unknown"}
              </div>
              <div className="mr-detail__card-sub">{record.patientEmail}</div>
            </div>

            <div className="mr-detail__card">
              <div className="mr-detail__card-label">
                <Stethoscope size={14} /> Clinician
              </div>
              <div className="mr-detail__card-val font-semibold">
                {record.doctorName || "Assigned Clinician"}
              </div>
              <div className="mr-detail__card-sub">
                {record.doctorSpecialization || "General Medicine"}
              </div>
            </div>

            <div className="mr-detail__card">
              <div className="mr-detail__card-label">
                <Calendar size={14} /> Recorded Date
              </div>
              <div className="mr-detail__card-val font-semibold">
                {new Date(record.recordDate).toLocaleDateString(undefined, {
                  year: "numeric",
                  month: "short",
                  day: "numeric",
                })}
              </div>
              <div className="mr-detail__card-sub">
                {record.followUpDate
                  ? `Follow-up: ${new Date(record.followUpDate).toLocaleDateString()}`
                  : "No follow-up"}
              </div>
            </div>
          </div>

          {/* Diagnosis & Clinical Findings */}
          <div className="form-section" style={{ marginTop: "8px" }}>
            <h5
              className="form-section__title"
              style={{ display: "flex", alignItems: "center", gap: "6px" }}
            >
              {record.recordType === "LabReport" ? (
                <FlaskConical size={15} />
              ) : record.recordType === "Prescription" ? (
                <Pill size={15} />
              ) : (
                <Activity size={15} />
              )}
              {record.recordType === "LabReport"
                ? " Laboratory Investigation"
                : record.recordType === "Prescription"
                  ? " Prescription Overview"
                  : record.recordType === "DischargeSummary"
                    ? " Discharge Clinical Evaluation"
                    : " Clinical Evaluation"}
            </h5>
            <div
              style={{
                background: "var(--bg-base)",
                padding: "14px 16px",
                borderRadius: "10px",
                border: "1px solid var(--border-default)",
              }}
            >
              <div
                style={{
                  fontSize: "0.8rem",
                  fontWeight: 600,
                  color: "var(--text-muted)",
                  textTransform: "uppercase",
                }}
              >
                {record.recordType === "LabReport"
                  ? "Investigation / Test Name"
                  : record.recordType === "Prescription"
                    ? "Medical Condition / Indication"
                    : record.recordType === "DischargeSummary"
                      ? "Final Discharge Diagnosis"
                      : record.recordType === "GeneralNote"
                        ? "Note Subject"
                        : "Primary Diagnosis"}
              </div>
              <div
                style={{
                  fontSize: "1.1rem",
                  fontWeight: 700,
                  color: "var(--clr-primary)",
                  marginTop: "2px",
                  marginBottom: "10px",
                }}
              >
                {record.diagnosis}
              </div>

              {record.symptoms && record.recordType !== "LabReport" && (
                <>
                  <div
                    style={{
                      fontSize: "0.8rem",
                      fontWeight: 600,
                      color: "var(--text-muted)",
                      textTransform: "uppercase",
                    }}
                  >
                    {record.recordType === "DischargeSummary"
                      ? "Hospital Course & Summary"
                      : record.recordType === "GeneralNote"
                        ? "Clinical Observations"
                        : "Symptoms & Observations"}
                  </div>
                  <div
                    style={{
                      fontSize: "0.92rem",
                      color: "var(--text-primary)",
                      lineHeight: 1.5,
                      marginTop: "2px",
                      marginBottom: "10px",
                    }}
                  >
                    {record.symptoms}
                  </div>
                </>
              )}

              <div
                style={{
                  fontSize: "0.8rem",
                  fontWeight: 600,
                  color: "var(--text-muted)",
                  textTransform: "uppercase",
                }}
              >
                {record.recordType === "LabReport"
                  ? "Diagnostic Impression & Interpretation"
                  : record.recordType === "Prescription"
                    ? "Instructions & Directions"
                    : record.recordType === "DischargeSummary"
                      ? "Post-Discharge Instructions"
                      : record.recordType === "GeneralNote"
                        ? "Recommendations & Plan"
                        : "Treatment Plan & Medical Advice"}
              </div>
              <div
                style={{
                  fontSize: "0.92rem",
                  color: "var(--text-primary)",
                  lineHeight: 1.5,
                  marginTop: "2px",
                }}
              >
                {record.treatmentPlan}
              </div>
            </div>
          </div>

          {/* Prescription & Lab Notes */}
          {(record.prescriptionNotes || record.labNotes) && (
            <div className="form-section">
              <h5
                className="form-section__title"
                style={{ display: "flex", alignItems: "center", gap: "6px" }}
              >
                <Pill size={15} /> Prescriptions & Diagnostics
              </h5>
              <div className="form-grid form-grid--2">
                {record.prescriptionNotes && (
                  <div
                    style={{
                      background: "var(--bg-base)",
                      padding: "14px",
                      borderRadius: "10px",
                      border: "1px solid var(--border-default)",
                    }}
                  >
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "6px",
                        fontSize: "0.8rem",
                        fontWeight: 700,
                        color: "var(--clr-accent)",
                        textTransform: "uppercase",
                        marginBottom: "6px",
                      }}
                    >
                      <Pill size={14} /> Prescribed Medications
                    </div>
                    <pre
                      style={{
                        margin: 0,
                        fontFamily: "var(--font-mono)",
                        fontSize: "0.84rem",
                        color: "var(--text-primary)",
                        whiteSpace: "pre-wrap",
                        lineHeight: 1.4,
                      }}
                    >
                      {record.prescriptionNotes}
                    </pre>
                  </div>
                )}

                {record.labNotes && (
                  <div
                    style={{
                      background: "rgba(8,145,178,0.06)",
                      padding: "14px",
                      borderRadius: "10px",
                      border: "1px solid rgba(8,145,178,0.2)",
                    }}
                  >
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "6px",
                        fontSize: "0.8rem",
                        fontWeight: 700,
                        color: "var(--clr-info)",
                        textTransform: "uppercase",
                        marginBottom: "6px",
                      }}
                    >
                      <FlaskConical size={14} /> Diagnostic Findings
                    </div>
                    <div
                      style={{
                        fontSize: "0.88rem",
                        color: "var(--text-primary)",
                        lineHeight: 1.4,
                      }}
                    >
                      {record.labNotes}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Read-Only Attachments Section */}
          <div className="form-section">
            <h5
              className="form-section__title"
              style={{ display: "flex", alignItems: "center", gap: "6px" }}
            >
              <Paperclip size={15} /> Attached Reports & Scans (
              {record.attachments?.length || 0})
            </h5>

            {record.attachments && record.attachments.length > 0 ? (
              <div
                className="mr-attachments-list"
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: "10px",
                }}
              >
                {record.attachments.map((att) => {
                  const isImage =
                    att.fileType?.includes("image") ||
                    /\.(jpe?g|png|webp|gif|bmp)$/i.test(att.fileName);
                  const fullUrl = getFullAttachmentUrl(att.fileUrl);
                  return (
                    <div
                      key={att.attachmentId}
                      className="mr-attachment-item"
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        padding: "12px 14px",
                        background: "var(--bg-base)",
                        border: "1px solid var(--border-default)",
                        borderRadius: "10px",
                        cursor: "pointer",
                        transition:
                          "border-color 0.2s ease, transform 0.15s ease",
                      }}
                      onClick={() => openPreview(att)}
                      title="Click to preview attachment"
                    >
                      <div
                        className="mr-attachment-item__left"
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: "12px",
                          minWidth: 0,
                        }}
                      >
                        {isImage ? (
                          <div
                            style={{
                              position: "relative",
                              width: "46px",
                              height: "46px",
                              borderRadius: "8px",
                              overflow: "hidden",
                              border: "1px solid var(--border-default)",
                              background: "#0f172a",
                              flexShrink: 0,
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "center",
                            }}
                          >
                            <img
                              src={fullUrl}
                              alt={att.fileName}
                              style={{
                                width: "100%",
                                height: "100%",
                                objectFit: "cover",
                              }}
                              onError={(e) => {
                                e.target.style.display = "none";
                                if (e.target.nextSibling)
                                  e.target.nextSibling.style.display = "flex";
                              }}
                            />
                            <div
                              style={{
                                display: "none",
                                width: "100%",
                                height: "100%",
                                alignItems: "center",
                                justifyContent: "center",
                                background: "rgba(99,102,241,0.1)",
                              }}
                            >
                              <ImageIcon size={20} color="var(--clr-accent)" />
                            </div>
                          </div>
                        ) : (
                          <div
                            style={{
                              width: "46px",
                              height: "46px",
                              borderRadius: "8px",
                              background: "rgba(99,102,241,0.1)",
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "center",
                              flexShrink: 0,
                            }}
                          >
                            <FileSpreadsheet
                              size={22}
                              color="var(--clr-primary)"
                            />
                          </div>
                        )}
                        <div style={{ minWidth: 0 }}>
                          <div
                            className="mr-attachment-item__name"
                            style={{
                              fontWeight: 600,
                              color: "var(--text-primary)",
                              fontSize: "0.9rem",
                              overflow: "hidden",
                              textOverflow: "ellipsis",
                              whiteSpace: "nowrap",
                            }}
                          >
                            {getCleanFileName(att.fileName)}
                          </div>
                          <div
                            className="mr-attachment-item__meta"
                            style={{
                              fontSize: "0.78rem",
                              color: "var(--text-muted)",
                              marginTop: "2px",
                            }}
                          >
                            <span
                              style={{
                                textTransform: "uppercase",
                                fontWeight: 600,
                                letterSpacing: "0.3px",
                              }}
                            >
                              {getCleanFileName(att.fileName)
                                .split(".")
                                .pop() || "FILE"}
                            </span>{" "}
                            • {formatFileSize(att.fileSize)} •{" "}
                            {new Date(
                              att.uploadedAt || Date.now(),
                            ).toLocaleDateString()}
                          </div>
                        </div>
                      </div>

                      <div
                        className="mr-attachment-item__actions"
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: "8px",
                          flexShrink: 0,
                        }}
                        onClick={(e) => e.stopPropagation()}
                      >
                        <Button
                          variant="secondary"
                          size="sm"
                          icon={Eye}
                          onClick={() => openPreview(att)}
                          title="Preview full image / document"
                        >
                          View
                        </Button>
                        <Button
                          variant="secondary"
                          size="sm"
                          icon={Download}
                          onClick={() => handleDownloadAttachment(att)}
                          title="Download original file"
                        >
                          Download
                        </Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div
                style={{
                  border: "1px dashed var(--border-default)",
                  borderRadius: "8px",
                  padding: "16px",
                  textAlign: "center",
                  background: "var(--bg-base)",
                }}
              >
                <div
                  style={{ fontSize: "0.85rem", color: "var(--text-muted)" }}
                >
                  No diagnostic files or scans attached to this medical record.
                </div>
              </div>
            )}
          </div>

          {/* Footer Actions */}
          <div className="form-actions" style={{ marginTop: "10px" }}>
            {canEdit && (
              <Button variant="secondary" onClick={() => onEdit(record)}>
                Edit Record
              </Button>
            )}
            <Button variant="primary" onClick={onClose}>
              Close
            </Button>
          </div>
        </div>
      </Modal>

      {/* In-App Attachment Lightbox & Preview Modal */}
      {previewAttachment && (
        <Modal
          open={Boolean(previewAttachment)}
          onClose={() => setPreviewAttachment(null)}
          title={
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "8px",
                maxWidth: "85%",
              }}
            >
              {previewAttachment.fileType?.includes("image") ||
              /\.(jpe?g|png|webp|gif|bmp)$/i.test(
                previewAttachment.fileName,
              ) ? (
                <ImageIcon size={20} color="var(--clr-accent)" />
              ) : (
                <FileSpreadsheet size={20} color="var(--clr-primary)" />
              )}
              <span
                style={{
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                  whiteSpace: "nowrap",
                }}
              >
                {getCleanFileName(previewAttachment.fileName)}
              </span>
            </div>
          }
          subtitle={`Record #${record.medicalRecordId} • ${previewAttachment.fileType || "Medical Attachment"} • ${formatFileSize(previewAttachment.fileSize)}`}
          size="lg"
        >
          <div
            style={{
              padding: "8px 0",
              display: "flex",
              flexDirection: "column",
              gap: "14px",
            }}
          >
            {previewAttachment.fileType?.includes("image") ||
            /\.(jpe?g|png|webp|gif|bmp)$/i.test(previewAttachment.fileName) ? (
              <div
                style={{ display: "flex", flexDirection: "column", gap: "8px" }}
              >
                {/* Image Toolbar */}
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    background: "var(--bg-base)",
                    padding: "8px 12px",
                    borderRadius: "8px",
                    border: "1px solid var(--border-default)",
                    flexWrap: "wrap",
                    gap: "8px",
                  }}
                >
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "6px",
                    }}
                  >
                    <Button
                      variant="secondary"
                      size="sm"
                      icon={ZoomOut}
                      onClick={() =>
                        setZoom((z) =>
                          Math.max(0.5, Number((z - 0.25).toFixed(2))),
                        )
                      }
                      title="Zoom Out"
                    />
                    <span
                      style={{
                        fontSize: "0.82rem",
                        fontWeight: 600,
                        minWidth: "46px",
                        textAlign: "center",
                      }}
                    >
                      {Math.round(zoom * 100)}%
                    </span>
                    <Button
                      variant="secondary"
                      size="sm"
                      icon={ZoomIn}
                      onClick={() =>
                        setZoom((z) =>
                          Math.min(3, Number((z + 0.25).toFixed(2))),
                        )
                      }
                      title="Zoom In"
                    />
                    <Button
                      variant="secondary"
                      size="sm"
                      icon={RotateCw}
                      onClick={() => setRotation((r) => (r + 90) % 360)}
                      title="Rotate 90°"
                    />
                    <Button
                      variant="secondary"
                      size="sm"
                      icon={RefreshCw}
                      onClick={() => {
                        setZoom(1);
                        setRotation(0);
                      }}
                      title="Reset View"
                    >
                      Reset
                    </Button>
                  </div>

                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "8px",
                    }}
                  >
                    <Button
                      variant="primary"
                      size="sm"
                      icon={Download}
                      onClick={() =>
                        handleDownloadAttachment(previewAttachment)
                      }
                      title="Download authentic image"
                    >
                      Download Image
                    </Button>
                  </div>
                </div>

                {/* Dark Canvas Viewer */}
                <div
                  style={{
                    background: "#090d16",
                    borderRadius: "12px",
                    border: "1px solid rgba(255,255,255,0.08)",
                    minHeight: "380px",
                    maxHeight: "65vh",
                    overflow: "auto",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    padding: "20px",
                    position: "relative",
                  }}
                >
                  {!imageLoadError ? (
                    <img
                      src={getFullAttachmentUrl(previewAttachment.fileUrl)}
                      alt={previewAttachment.fileName}
                      style={{
                        transform: `scale(${zoom}) rotate(${rotation}deg)`,
                        transformOrigin: "center center",
                        transition: "transform 0.15s ease-out",
                        maxWidth: zoom > 1 ? "none" : "100%",
                        maxHeight: zoom > 1 ? "none" : "58vh",
                        objectFit: "contain",
                        borderRadius: "6px",
                        boxShadow: "0 10px 30px rgba(0,0,0,0.6)",
                        cursor: zoom > 1 ? "grab" : "default",
                      }}
                      onError={() => setImageLoadError(true)}
                    />
                  ) : (
                    /* Fallback diagnostic document view */
                    <div
                      style={{
                        background: "#ffffff",
                        color: "#0f172a",
                        borderRadius: "10px",
                        padding: "24px",
                        maxWidth: "520px",
                        width: "100%",
                        boxShadow: "0 12px 36px rgba(0,0,0,0.5)",
                        border: "1px solid #cbd5e1",
                        fontFamily: "system-ui, sans-serif",
                      }}
                    >
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                          borderBottom: "2px solid #0284c7",
                          paddingBottom: "12px",
                          marginBottom: "16px",
                        }}
                      >
                        <div>
                          <div
                            style={{
                              fontSize: "1.1rem",
                              fontWeight: 800,
                              color: "#0284c7",
                              letterSpacing: "-0.3px",
                            }}
                          >
                            MEDICORE HEALTH ARCHIVES
                          </div>
                          <div
                            style={{ fontSize: "0.75rem", color: "#64748b" }}
                          >
                            Diagnostic Radiology & Clinical Imaging
                          </div>
                        </div>
                        <Badge variant="primary">
                          Record #{record.medicalRecordId}
                        </Badge>
                      </div>

                      <div
                        style={{
                          fontSize: "0.85rem",
                          lineHeight: "1.6",
                          marginBottom: "14px",
                        }}
                      >
                        <div>
                          <strong>Document:</strong>{" "}
                          {previewAttachment.fileName}
                        </div>
                        <div>
                          <strong>Patient:</strong> {record.patientName} (
                          {record.patientEmail})
                        </div>
                        <div>
                          <strong>Record Type:</strong> {record.recordType} (
                          {new Date(record.recordDate).toLocaleDateString()})
                        </div>
                        <div>
                          <strong>Condition / Diagnosis:</strong>{" "}
                          {record.diagnosis}
                        </div>
                      </div>

                      <div
                        style={{
                          background: "#f8fafc",
                          border: "1px dashed #94a3b8",
                          borderRadius: "8px",
                          padding: "14px",
                          textAlign: "center",
                          marginBottom: "16px",
                        }}
                      >
                        <ImageIcon
                          size={36}
                          color="#0284c7"
                          style={{ margin: "0 auto 8px", display: "block" }}
                        />
                        <div
                          style={{
                            fontSize: "0.85rem",
                            fontWeight: 600,
                            color: "#334155",
                          }}
                        >
                          Verified Diagnostic Scan Record
                        </div>
                        <div
                          style={{
                            fontSize: "0.75rem",
                            color: "#64748b",
                            marginTop: "4px",
                          }}
                        >
                          This image was archived in the MediCore Diagnostic
                          System.
                        </div>
                      </div>

                      <div
                        style={{
                          display: "flex",
                          gap: "8px",
                          justifyContent: "flex-end",
                        }}
                      >
                        <Button
                          variant="secondary"
                          size="sm"
                          icon={RefreshCw}
                          onClick={() => setImageLoadError(false)}
                        >
                          Retry
                        </Button>
                        <Button
                          variant="primary"
                          size="sm"
                          icon={Download}
                          onClick={() =>
                            handleDownloadAttachment(previewAttachment)
                          }
                        >
                          Download Image
                        </Button>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            ) : previewAttachment.fileType?.includes("pdf") ||
              /\.pdf$/i.test(previewAttachment.fileName) ? (
              /* PDF embedded iframe viewer */
              <div
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: "10px",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    justifyContent: "flex-end",
                    gap: "8px",
                  }}
                >
                  <Button
                    variant="primary"
                    size="sm"
                    icon={Download}
                    onClick={() => handleDownloadAttachment(previewAttachment)}
                  >
                    Download PDF
                  </Button>
                </div>
                <iframe
                  src={
                    (typeof window !== "undefined" &&
                      sessionStorage.getItem(
                        `med_preview_${previewAttachment.fileName}`,
                      )) ||
                    getFullAttachmentUrl(previewAttachment.fileUrl)
                  }
                  title={previewAttachment.fileName}
                  style={{
                    width: "100%",
                    height: "65vh",
                    border: "1px solid var(--border-default)",
                    borderRadius: "10px",
                    background: "#fff",
                  }}
                />
              </div>
            ) : (
              /* Other document types */
              <div
                style={{
                  background: "var(--bg-base)",
                  border: "1px solid var(--border-default)",
                  borderRadius: "10px",
                  padding: "24px",
                  textAlign: "center",
                }}
              >
                <FileSpreadsheet
                  size={42}
                  color="var(--clr-primary)"
                  style={{ margin: "0 auto 12px", display: "block" }}
                />
                <h4 style={{ margin: "0 0 8px", fontSize: "1rem" }}>
                  {previewAttachment.fileName}
                </h4>
                <p
                  style={{
                    fontSize: "0.85rem",
                    color: "var(--text-muted)",
                    margin: "0 0 16px",
                  }}
                >
                  {previewAttachment.fileType} •{" "}
                  {formatFileSize(previewAttachment.fileSize)}
                </p>
                <div
                  style={{
                    display: "flex",
                    gap: "10px",
                    justifyContent: "center",
                  }}
                >
                  <Button
                    variant="primary"
                    icon={Download}
                    onClick={() => handleDownloadAttachment(previewAttachment)}
                  >
                    Download Document
                  </Button>
                </div>
              </div>
            )}

            {/* Footer Close */}
            <div
              style={{
                display: "flex",
                justifyContent: "flex-end",
                borderTop: "1px solid var(--border-default)",
                paddingTop: "10px",
              }}
            >
              <Button
                variant="secondary"
                onClick={() => setPreviewAttachment(null)}
              >
                Close Preview
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </>
  );
}
