import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './base-api.service';
import { API_ENDPOINTS } from '../constants/api.constants';
import { HelpCenterDto } from '../models/help.model';

@Injectable({ providedIn: 'root' })
export class HelpService extends BaseApiService {
  getHelpCenter(): Observable<HelpCenterDto> {
    return this.get<HelpCenterDto>(API_ENDPOINTS.help.faqs);
  }
}
