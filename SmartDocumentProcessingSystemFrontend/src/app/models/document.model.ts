export type DocumentType = 'Unknown' | 'Invoice' | 'PurchaseOrder';
export type DocumentStatus = 'Uploaded' | 'NeedsReview' | 'Validated' | 'Rejected';
export type ValidationSeverity = 'Warning' | 'Error';

export interface LineItem {
  id: number;
  description: string;
  quantity: number | null;
  unitPrice: number | null;
  taxRate: number | null;
  tax: number | null;
  total: number | null;
}

export interface ValidationIssue {
  id: number;
  severity: ValidationSeverity;
  fieldPath: string;
  message: string;
  expectedValue: string | null;
  actualValue: string | null;
}

export interface DocumentSummary {
  id: number;
  originalFileName: string;
  type: DocumentType;
  supplier: string | null;
  documentNumber: string | null;
  currency: string | null;
  total: number | null;
  status: DocumentStatus;
  errorCount: number;
  warningCount: number;
  createdAtUtc: string;
}

export interface DocumentDetail extends DocumentSummary {
  fileExtension: string;
  issueDate: string | null;
  dueDate: string | null;
  subtotal: number | null;
  tax: number | null;
  lineItems: LineItem[];
  validationIssues: ValidationIssue[];
  updatedAtUtc: string;
}

export interface DashboardSummary {
  statusCounts: Partial<Record<DocumentStatus, number>>;
  errorCount: number;
  warningCount: number;
  totalsByCurrency: Record<string, number>;
}

export interface UpdateDocumentRequest {
  type: DocumentType;
  supplier: string | null;
  documentNumber: string | null;
  issueDate: string | null;
  dueDate: string | null;
  currency: string | null;
  subtotal: number | null;
  tax: number | null;
  total: number | null;
  lineItems: LineItem[];
}
