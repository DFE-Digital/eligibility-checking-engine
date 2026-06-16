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
  