import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import {
  DashboardSummary,
  DocumentDetail,
  DocumentSummary,
  UpdateDocumentRequest,
} from '../models/document.model';

@Injectable({ providedIn: 'root' })
export class DocumentApiService {
  private readonly baseUrl = 'http://localhost:5183/api/documents';

  constructor(private readonly http: HttpClient) {}

  getDocuments() {
    return this.http.get<DocumentSummary[]>(this.baseUrl);
  }

  getSummary() {
    return this.http.get<DashboardSummary>(`${this.baseUrl}/summary`);
  }

  getDocument(id: number) {
    return this.http.get<DocumentDetail>(`${this.baseUrl}/${id}`);
  }

  upload(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<DocumentDetail>(`${this.baseUrl}/upload`, formData);
  }

  importSamples(refreshExisting = true) {
    return this.http.post<DocumentDetail[]>(`${this.baseUrl}/import-samples?refreshExisting=${refreshExisting}`, {});
  }

  update(id: number, request: UpdateDocumentRequest) {
    return this.http.patch<DocumentDetail>(`${this.baseUrl}/${id}`, request);
  }

  validate(id: number) {
    return this.http.post<DocumentDetail>(`${this.baseUrl}/${id}/validate`, {});
  }

  confirm(id: number) {
    return this.http.post<DocumentDetail>(`${this.baseUrl}/${id}/confirm`, {});
  }

  reject(id: number) {
    return this.http.post<DocumentDetail>(`${this.baseUrl}/${id}/reject`, {});
  }
}
