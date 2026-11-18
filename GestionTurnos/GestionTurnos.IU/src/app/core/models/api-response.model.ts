export interface ApiResponse<T = void> {
  readonly success: boolean;
  readonly message: string;
  readonly data?: T;
}

export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  readonly code?: string;
  readonly traceId?: string;
  readonly errors?: Record<string, string[]>;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
  readonly pageNumber: number;
  readonly pageSize: number;
}
