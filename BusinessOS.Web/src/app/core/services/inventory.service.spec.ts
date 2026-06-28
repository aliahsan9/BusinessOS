import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InventoryService } from './inventory.service';
import { API_ENDPOINTS } from '../constants/api.constants';

describe('InventoryService', () => {
  let service: InventoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), InventoryService],
    });
    service = TestBed.inject(InventoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch inventory analytics', () => {
    service.getAnalytics().subscribe((data) => {
      expect(data.totalProducts).toBe(10);
      expect(data.inventoryValue).toBe(5000);
    });

    const req = httpMock.expectOne((r) => r.url.includes(API_ENDPOINTS.inventory.analytics));
    req.flush({
      totalProducts: 10,
      totalStockQuantity: 100,
      lowStockCount: 2,
      outOfStockCount: 1,
      inventoryValue: 5000,
      mostSoldProducts: [],
      leastSoldProducts: [],
      stockMovementTrends: [],
    });
  });
});
