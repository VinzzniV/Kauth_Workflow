import type { RequirementSelectionState } from "../types/workflow";

export const REQUIREMENT_KEYS = {
  adUserRequested: "ad_user_requested",
  comparisonUserAvailable: "comparison_user_available",
  comparisonUserName: "comparison_user_name",
  hardwareRequested: "hardware_requested",
  hardwareAvailable: "hardware_available",
  phoneRequested: "phone_requested",
  hardwareType: "hardware_type",
  laptopVpnType: "laptop_vpn_type",
  internalDriveAccessRequested: "internal_drive_access_requested",
  internalDriveAccessRoles: "internal_drive_access_roles",
} as const;

type VisibilityDependencyRule = {
  dependencyKey: string;
  kind: "boolean_true" | "selected_option_value";
  expectedValue?: string;
  missingResult?: boolean;
};

type SelectionResetTarget = {
  requirementKey: string;
  patch: Partial<RequirementSelectionState>;
};

type ValidationRule = {
  kind: "text_required" | "single_select_required" | "multi_select_required";
  message: string;
};

export const requirementVisibilityRules: Partial<Record<string, VisibilityDependencyRule[]>> = {
  [REQUIREMENT_KEYS.comparisonUserAvailable]: [
    {
      dependencyKey: REQUIREMENT_KEYS.adUserRequested,
      kind: "boolean_true",
      missingResult: true,
    },
  ],
  [REQUIREMENT_KEYS.comparisonUserName]: [
    {
      dependencyKey: REQUIREMENT_KEYS.adUserRequested,
      kind: "boolean_true",
      missingResult: false,
    },
    {
      dependencyKey: REQUIREMENT_KEYS.comparisonUserAvailable,
      kind: "boolean_true",
      missingResult: false,
    },
  ],
  [REQUIREMENT_KEYS.hardwareAvailable]: [
    {
      dependencyKey: REQUIREMENT_KEYS.hardwareRequested,
      kind: "boolean_true",
      missingResult: false,
    },
  ],
  [REQUIREMENT_KEYS.hardwareType]: [
    {
      dependencyKey: REQUIREMENT_KEYS.hardwareRequested,
      kind: "boolean_true",
      missingResult: true,
    },
  ],
  [REQUIREMENT_KEYS.phoneRequested]: [
    {
      dependencyKey: REQUIREMENT_KEYS.hardwareRequested,
      kind: "boolean_true",
      missingResult: true,
    },
  ],
  [REQUIREMENT_KEYS.internalDriveAccessRoles]: [
    {
      dependencyKey: REQUIREMENT_KEYS.internalDriveAccessRequested,
      kind: "boolean_true",
      missingResult: true,
    },
  ],
  [REQUIREMENT_KEYS.laptopVpnType]: [
    {
      dependencyKey: REQUIREMENT_KEYS.hardwareRequested,
      kind: "boolean_true",
      missingResult: true,
    },
    {
      dependencyKey: REQUIREMENT_KEYS.hardwareType,
      kind: "selected_option_value",
      expectedValue: "laptop",
      missingResult: false,
    },
  ],
};

export const requirementValidationRules: Partial<Record<string, ValidationRule>> = {
  [REQUIREMENT_KEYS.comparisonUserName]: {
    kind: "text_required",
    message: "Bitte den Referenzuser angeben.",
  },
  [REQUIREMENT_KEYS.internalDriveAccessRoles]: {
    kind: "multi_select_required",
    message: "Bitte mindestens eine Funktion für die Laufwerksrechte auswählen.",
  },
  [REQUIREMENT_KEYS.laptopVpnType]: {
    kind: "single_select_required",
    message: "Bitte auswählen, ob der Laptop mit VPN oder ohne VPN benötigt wird.",
  },
};

export const requirementBooleanResetRules: Partial<Record<string, SelectionResetTarget[]>> = {
  [REQUIREMENT_KEYS.adUserRequested]: [
    {
      requirementKey: REQUIREMENT_KEYS.comparisonUserAvailable,
      patch: { valueBoolean: null },
    },
    {
      requirementKey: REQUIREMENT_KEYS.comparisonUserName,
      patch: { valueText: "" },
    },
  ],
  [REQUIREMENT_KEYS.comparisonUserAvailable]: [
    {
      requirementKey: REQUIREMENT_KEYS.comparisonUserName,
      patch: { valueText: "" },
    },
  ],
  [REQUIREMENT_KEYS.hardwareRequested]: [
    {
      requirementKey: REQUIREMENT_KEYS.hardwareAvailable,
      patch: { valueBoolean: null },
    },
    {
      requirementKey: REQUIREMENT_KEYS.phoneRequested,
      patch: { valueBoolean: null },
    },
    {
      requirementKey: REQUIREMENT_KEYS.hardwareType,
      patch: { selectedOptionId: null, selectedOptionIds: [] },
    },
    {
      requirementKey: REQUIREMENT_KEYS.laptopVpnType,
      patch: { selectedOptionId: null, selectedOptionIds: [] },
    },
  ],
  [REQUIREMENT_KEYS.internalDriveAccessRequested]: [
    {
      requirementKey: REQUIREMENT_KEYS.internalDriveAccessRoles,
      patch: { selectedOptionIds: [] },
    },
  ],
};

export const requirementSingleSelectResetRules: Partial<
  Record<string, { keepSelectedOptionValues: string[]; targets: SelectionResetTarget[] }>
> = {
  [REQUIREMENT_KEYS.hardwareType]: {
    keepSelectedOptionValues: ["laptop"],
    targets: [
      {
        requirementKey: REQUIREMENT_KEYS.laptopVpnType,
        patch: { selectedOptionId: null, selectedOptionIds: [] },
      },
    ],
  },
};
