import {
  Edit3,
  Save,
  X,
  Trash2,
  AlertCircle,
  User,
  Phone,
  Mail,
  CreditCard,
  Stethoscope,
  ShieldCheck,
  Award,
  Lock,
  CheckCircle2,
} from "lucide-react";
import { useEffect, useState } from "react";
import Button from "../../../components/Button";
import Badge from "../../../components/Badge";
import { useAuth } from "../../auth/AuthContext";
import {
  getMyDoctorProfile,
  updateMyDoctorProfile,
  requestDoctorDeletion,
} from "../services/doctorApi";
import { getInitials, nameToGradient } from "../../../lib/utils";
import "./DoctorProfilePage.css";

const editableFields = (profile) => ({
  firstName: profile.firstName || "",
  lastName: profile.lastName || "",
  phoneNumber: profile.phoneNumber || "",
});

export default function DoctorProfilePage() {
  const { updateUser } = useAuth();
  const [profile, setProfile] = useState(null);
  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    phoneNumber: "",
  });
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [saving, setSaving] = useState(false);
  const [requestingDeletion, setRequestingDeletion] = useState(false);

  useEffect(() => {
    getMyDoctorProfile()
      .then((data) => {
        setProfile(data);
        setForm(editableFields(data));
      })
      .catch((requestError) =>
        setError(
          requestError.response?.data?.message || "Unable to load profile.",
        ),
      );
  }, []);

  const beginEditing = () => {
    setForm(editableFields(profile));
    setError("");
    setSuccess("");
    setEditing(true);
  };

  const cancelEditing = () => {
    setForm(editableFields(profile));
    setError("");
    setEditing(false);
  };

  const changeField = (field, value) =>
    setForm((current) => ({ ...current, [field]: value }));

  const save = async () => {
    if (!editing) return;

    setError("");
    setSuccess("");
    if (!form.firstName.trim() || !form.lastName.trim()) {
      setError("First name and second name are required.");
      return;
    }
    if (
      !/^(?:07\d{8}|\+947\d{8})$/.test(form.phoneNumber.replace(/[ -]/g, ""))
    ) {
      setError(
        "Enter a valid Sri Lankan mobile number: 07XXXXXXXX or +947XXXXXXXX.",
      );
      return;
    }

    setSaving(true);
    try {
      const updated = await updateMyDoctorProfile({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        phoneNumber: form.phoneNumber.trim(),
      });
      setProfile(updated);
      setForm(editableFields(updated));
      updateUser({ fullName: updated.fullName });
      setEditing(false);
      setSuccess("Profile updated successfully.");
    } catch (requestError) {
      const data = requestError.response?.data;
      const validationMessage = data?.errors
        ? Object.values(data.errors).flat().find(Boolean)
        : null;
      setError(
        data?.message || validationMessage || "Unable to update profile.",
      );
    } finally {
      setSaving(false);
    }
  };

  const handleRequestDeletion = async () => {
    const reason = window.prompt(
      "Please enter a reason for requesting account deletion (optional):",
      "",
    );
    if (reason === null) return;

    setRequestingDeletion(true);
    setError("");
    setSuccess("");
    try {
      const result = await requestDoctorDeletion(reason);
      setProfile((prev) => ({
        ...prev,
        registrationStatus: "DeletionPending",
      }));
      setSuccess(
        result.message ||
          "Account deletion request submitted to Admin for approval.",
      );
    } catch (requestError) {
      setError(
        requestError.response?.data?.message ||
          "Failed to submit deletion request.",
      );
    } finally {
      setRequestingDeletion(false);
    }
  };

  const isDeletionPending = profile?.registrationStatus === "DeletionPending";
  const fullName = profile
    ? `Dr. ${profile.firstName || ""} ${profile.lastName || ""}`.trim()
    : "";

  if (!profile) {
    return (
      <div className="doctor-profile-page">
        <p className="text-muted">{error || "Loading profile..."}</p>
      </div>
    );
  }

  return (
    <div className="doctor-profile-page animate-fade-in">
      {/* Hero Header */}
      <div className="glass-card doc-profile-hero">
        <div
          className="doc-profile-hero__banner"
          style={{ background: nameToGradient(fullName) }}
        >
          <div className="doc-profile-hero__avatar">
            {getInitials(fullName)}
          </div>
        </div>
        <div className="doc-profile-hero__content">
          <div className="doc-profile-hero__main">
            <div>
              <h1 className="doc-profile-hero__title">{fullName}</h1>
              <p className="doc-profile-hero__subtitle">
                <Stethoscope size={15} className="mr-1.5 inline text-primary" />
                {profile.specialization}
              </p>
            </div>
            <Badge variant={isDeletionPending ? "warning" : "success"}>
              {profile.registrationStatus}
            </Badge>
          </div>
        </div>
      </div>

      {/* Status Warning Banner */}
      {isDeletionPending && (
        <div className="doc-profile-alert doc-profile-alert--danger">
          <div className="doc-profile-alert__content">
            <AlertCircle size={22} className="doc-profile-alert__icon" />
            <div className="doc-profile-alert__body">
              <strong>Account Deletion Pending</strong>
              <p>
                You have submitted an account deletion request. An administrator
                will review and process your request soon.
              </p>
            </div>
          </div>
        </div>
      )}

      {error && (
        <div className="doc-profile-alert doc-profile-alert--danger">
          <AlertCircle size={20} className="doc-profile-alert__icon" />
          <div className="doc-profile-alert__body">
            <span>{error}</span>
          </div>
        </div>
      )}

      {success && (
        <div className="doc-profile-alert doc-profile-alert--success">
          <CheckCircle2 size={20} className="doc-profile-alert__icon" />
          <div className="doc-profile-alert__body">
            <span>{success}</span>
          </div>
        </div>
      )}

      <div className="doc-profile-grid-container">
        {/* Personal Details Section */}
        <section className="glass-card doc-profile-section">
          <div className="doc-profile-section__header">
            <User size={18} className="doc-profile-section__icon" />
            <div>
              <h3>Personal & Contact Info</h3>
              <p>Update your editable identity details</p>
            </div>
          </div>

          <div className="doc-profile-fields">
            <ProfileField
              icon={User}
              label="First Name"
              value={form.firstName}
              editable={editing && !isDeletionPending}
              onChange={(value) => changeField("firstName", value)}
            />
            <ProfileField
              icon={User}
              label="Second Name"
              value={form.lastName}
              editable={editing && !isDeletionPending}
              onChange={(value) => changeField("lastName", value)}
            />
            <ProfileField
              icon={Phone}
              label="Phone Number"
              value={form.phoneNumber}
              editable={editing && !isDeletionPending}
              onChange={(value) => changeField("phoneNumber", value)}
            />
            <ProfileField
              icon={Mail}
              label="Email Address"
              value={profile.email}
              locked
            />
          </div>

          <div className="doc-profile-section__footer">
            {!editing ? (
              <Button
                type="button"
                variant="primary"
                icon={Edit3}
                disabled={isDeletionPending}
                onClick={beginEditing}
              >
                Edit Information
              </Button>
            ) : (
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="primary"
                  icon={Save}
                  loading={saving}
                  onClick={save}
                >
                  Save Changes
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  icon={X}
                  disabled={saving}
                  onClick={cancelEditing}
                >
                  Cancel
                </Button>
              </div>
            )}
          </div>
        </section>

        {/* Medical & Credentials Section */}
        <section className="glass-card doc-profile-section">
          <div className="doc-profile-section__header">
            <Award size={18} className="doc-profile-section__icon" />
            <div>
              <h3>Medical Verification & Credentials</h3>
              <p>System verified medical license records</p>
            </div>
          </div>

          <div className="doc-profile-fields">
            <ProfileField
              icon={CreditCard}
              label="National Identity Card (NIC)"
              value={profile.nic}
              locked
            />
            <ProfileField
              icon={Stethoscope}
              label="Medical Specialization"
              value={profile.specialization}
              locked
            />
            <ProfileField
              icon={Award}
              label="SLMC License Number"
              value={profile.slmcLicenseNumber}
              locked
            />
            <ProfileField
              icon={ShieldCheck}
              label="Account Verification Status"
              value={profile.registrationStatus}
              locked
            />
          </div>

          {!editing && !isDeletionPending && (
            <div className="doc-profile-danger-zone">
              <div className="danger-zone-info">
                <h4>Account Removal</h4>
                <p>Request account removal from admin</p>
              </div>
              <Button
                type="button"
                variant="danger"
                size="sm"
                icon={Trash2}
                loading={requestingDeletion}
                onClick={handleRequestDeletion}
              >
                Request Deletion
              </Button>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

function ProfileField({
  icon: Icon,
  label,
  value,
  editable = false,
  locked = false,
  onChange,
}) {
  return (
    <div
      className={`doc-profile-field ${editable ? "doc-profile-field--editable" : ""} ${locked ? "doc-profile-field--locked" : ""}`}
    >
      <label className="doc-profile-field__label">
        {Icon && <Icon size={14} />}
        <span>{label}</span>
        {locked && (
          <Lock
            size={12}
            className="ml-auto text-muted"
            title="Field is locked for security"
          />
        )}
      </label>
      <input
        type="text"
        className="doc-profile-field__input"
        value={value || ""}
        readOnly={!editable}
        disabled={locked || !editable}
        onChange={(e) => editable && onChange?.(e.target.value)}
      />
    </div>
  );
}
