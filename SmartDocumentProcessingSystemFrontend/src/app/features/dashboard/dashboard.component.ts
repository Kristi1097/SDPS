import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  DashboardSummary,
  DocumentDetail,
  DocumentStatus,
  DocumentSummary,
  DocumentType,
  LineItem,
  UpdateDocumentRequest,
} from '../../models/document.model';
import { DocumentApiService } from '../../services/document-api.service';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  documents: DocumentSummary[] = [];
  editableDocument: DocumentDetail | null = null;
  summary: DashboardSummary | null = null;
  loading = false;
  busyAction = '';
  actionMessage = '';
  errorMessage = '';
  lastSuccessfulAction = '';
  private successTimer: ReturnType<typeof setTimeout> | null = null;
  private messageTimer: ReturnType<typeof setTimeout> | null = null;
  readonly documentTypes: DocumentType[] = ['Unknown', 'Invoice', 'PurchaseOrder'];
  readonly statuses: DocumentStatus[] = ['Uploaded', 'NeedsReview', 'Validated', 'Rejected'];

  constructor(private readonly api: DocumentApiService) {}

  ngOnInit() {
    this.refresh();
  }

  refresh(showLoading = true) {
    this.errorMessage = '';
    this.loading = showLoading;
    this.api.getDocuments().pipe(finalize(() => (this.loading = false))).subscribe({
      next: (documents) => {
        this.documents = documents;
      },
      error: () => {
        this.errorMessage = 'Could not load documents. Check that the backend is running on localhost:5183.';
      },
    });

    this.api.getSummary().subscribe({
      next: (summary) => (this.summary = summary),
      error: () => (this.summary = null),
    });
  }

  selectDocument(document: DocumentSummary) {
    this.actionMessage = '';
    this.api.getDocument(document.id).subscribe({
      next: (detail) => {
        this.editableDocument = structuredClone(detail);
      },
      error: () => (this.errorMessage = 'Could not load the selected document.'),
    });
  }

  uploadFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.startAction('upload');
    this.api.upload(file).pipe(finalize(() => this.finishAction('upload'))).subscribe({
      next: (document) => {
        this.showSuccess('upload', `Uploaded ${document.originalFileName}.`);
        input.value = '';
        this.refresh(false);
        this.editableDocument = structuredClone(document);
      },
      error: () => {
        this.errorMessage = 'Upload failed.';
      },
    });
  }

  importSamples() {
    this.startAction('import');
    this.api.importSamples().pipe(finalize(() => this.finishAction('import'))).subscribe({
      next: (documents) => {
        this.showSuccess('import', `Imported ${documents.length} sample documents.`);
        this.refresh(false);
      },
      error: () => {
        this.errorMessage = 'Sample import failed.';
      },
    });
  }

  saveCorrections() {
    if (!this.editableDocument) {
      return;
    }

    const request: UpdateDocumentRequest = {
      type: this.editableDocument.type,
      supplier: this.editableDocument.supplier?.trim() || null,
      documentNumber: this.editableDocument.documentNumber?.trim() || null,
      issueDate: this.editableDocument.issueDate || null,
      dueDate: this.editableDocument.dueDate || null,
      currency: this.editableDocument.currency?.trim() || null,
      subtotal: this.editableDocument.subtotal,
      tax: this.editableDocument.tax,
      total: this.editableDocument.total,
      lineItems: this.editableDocument.lineItems,
    };

    this.startAction('save');
    this.api.update(this.editableDocument.id, request).pipe(finalize(() => this.finishAction('save'))).subscribe({
      next: (document) => this.afterDocumentAction(document, 'Corrections saved and validation refreshed.', 'save'),
      error: () => (this.errorMessage = 'Could not save corrections.'),
    });
  }

  revalidate() {
    if (!this.editableDocument) {
      return;
    }

    this.startAction('validate');
    this.api.validate(this.editableDocument.id).pipe(finalize(() => this.finishAction('validate'))).subscribe({
      next: (document) => this.afterDocumentAction(document, 'Validation refreshed.', 'validate'),
      error: () => (this.errorMessage = 'Validation failed.'),
    });
  }

  confirm() {
    if (!this.editableDocument) {
      return;
    }

    this.startAction('confirm');
    this.api.confirm(this.editableDocument.id).pipe(finalize(() => this.finishAction('confirm'))).subscribe({
      next: (document) => this.afterDocumentAction(document, 'Document confirmed when no blocking errors remain.', 'confirm'),
      error: () => (this.errorMessage = 'Could not confirm document.'),
    });
  }

  reject() {
    if (!this.editableDocument) {
      return;
    }

    this.startAction('reject');
    this.api.reject(this.editableDocument.id).pipe(finalize(() => this.finishAction('reject'))).subscribe({
      next: (document) => this.afterDocumentAction(document, 'Document rejected.', 'reject'),
      error: () => (this.errorMessage = 'Could not reject document.'),
    });
  }

  addLineItem() {
    this.editableDocument?.lineItems.push({
      id: 0,
      description: '',
      quantity: null,
      unitPrice: null,
      taxRate: null,
      tax: null,
      total: null,
    });
  }

  removeLineItem(index: number) {
    this.editableDocument?.lineItems.splice(index, 1);
  }

  statusCount(status: DocumentStatus): number {
    return this.summary?.statusCounts?.[status] ?? 0;
  }

  issueClass(status: DocumentStatus) {
    return `status status-${status.toLowerCase()}`;
  }

  currencyTotals() {
    return Object.entries(this.summary?.totalsByCurrency ?? {});
  }

  trackDocument(_: number, document: DocumentSummary) {
    return document.id;
  }

  trackLineItem(index: number, item: LineItem) {
    return item.id || index;
  }

  isActionDone(action: string) {
    return this.lastSuccessfulAction === action;
  }

  isActionBusy(action: string) {
    return this.busyAction === action;
  }

  private afterDocumentAction(document: DocumentDetail, message: string, action: string) {
    this.errorMessage = '';
    this.editableDocument = structuredClone(document);
    this.showSuccess(action, message);
    this.refresh(false);
  }

  private startAction(action: string) {
    this.busyAction = action;
    this.errorMessage = '';
  }

  private finishAction(action: string) {
    if (this.busyAction === action) {
      this.busyAction = '';
    }
  }

  private showSuccess(action: string, message: string) {
    this.actionMessage = message;
    this.lastSuccessfulAction = action;

    if (this.successTimer) {
      clearTimeout(this.successTimer);
    }
    if (this.messageTimer) {
      clearTimeout(this.messageTimer);
    }

    this.successTimer = setTimeout(() => {
      this.lastSuccessfulAction = '';
      this.successTimer = null;
    }, 1600);

    this.messageTimer = setTimeout(() => {
      this.actionMessage = '';
      this.messageTimer = null;
    }, 2500);
  }
}
