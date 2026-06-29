// cypress/support/interfaces.ts

export interface LocalAuthority {
  id: number;
  name: string;
}

export interface Establishment {
  id: number;
  name: string;
  localAuthority: LocalAuthority;
}

export interface ApplicationData {
  id: string;
  reference: string;
  establishment: Establishment;
  parentFirstName: string;
  parentLastName: string;
  parentNationalInsuranceNumber: string;
  parentNationalAsylumSeekerServiceNumber: string;
  parentDateOfBirth: string;
  childFirstName: string;
  childLastName: string;
  childDateOfBirth: string;
  status: string;
  tier: string;
  user: any;
}

// Bulk Check response models
export interface StatusValue {
  status: string;
}

export interface CheckEligibilityResponseBulkLinks {
  get_Progress_Check: string;
  get_BulkCheck_Results: string;
  get_BulkCheck_Status: string;
}

export interface CheckEligibilityResponseBulk {
  data: StatusValue;
  links: CheckEligibilityResponseBulkLinks;
}

export interface BulkResultItem {
  clientIdentifier: string;
  status: string;
}