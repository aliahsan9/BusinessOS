import { PaginationParams } from './pagination.model';

export interface InventorySummary {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  currentStock: number;
  reorderLevel: number;
  minimumStockLevel: number;
  maximumStockLevel: number;
  suggestedReorderQuantity: number;
  isLowStock: boolean;
  isOutOfStock: boolean;
}

export interface InventoryDetail extends InventorySummary {
  lastUpdated: string;
}

export interface StockTransaction {
  id: string;
  productId: string;
  productName: string;
  transactionType: string;
  quantity: number;
  previousStock: number;
  newStock: number;
  referenceNumber?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface InventoryAnalytics {
  totalProducts: number;
  totalStockQuantity: number;
  lowStockCount: number;
  outOfStockCount: number;
  inventoryValue: number;
  mostSoldProducts: ProductStockMovement[];
  leastSoldProducts: ProductStockMovement[];
  stockMovementTrends: StockMovementTrend[];
}

export interface ProductStockMovement {
  productId: string;
  productName: string;
  totalQuantityMoved: number;
}

export interface StockMovementTrend {
  date: string;
  totalIn: number;
  totalOut: number;
}

export interface UpdateInventoryRequest {
  minimumStockLevel: number;
  maximumStockLevel: number;
  reorderLevel: number;
}

export interface StockChangeRequest {
  productId: string;
  quantity: number;
  referenceNumber?: string | null;
  notes?: string | null;
}

export interface StockAdjustmentRequest extends StockChangeRequest {
  transactionType: string;
}

export interface InventoryQueryParams extends PaginationParams {
  lowStock?: boolean;
  outOfStock?: boolean;
}

export interface StockTransactionQueryParams extends PaginationParams {
  productId?: string;
  transactionType?: string;
}
