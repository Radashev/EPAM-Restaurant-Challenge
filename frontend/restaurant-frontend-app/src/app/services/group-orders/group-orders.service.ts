
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { switchMap } from 'rxjs';
import { map } from 'rxjs';
import { GuestInvite, GroupInviteToken } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';

@Injectable({
  providedIn: 'root',
})
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);

  getInviteToken(reservationId: string) {
    return this.http.get<GroupInviteToken>(
      `${this.baseUrl}/reservations/${reservationId}/invite-token`
    );
  }

  getInvite(token: string) {
    return this.http.get<GuestInvite>(
      `${this.baseUrl}/guest/invites/${token}`
    );
  }

  getGroupSessions(groupReservationId: string) {
    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/sessions`
    );
  }

  getGuestSessionId(reservationId: string) {
    return this.http.get<{ guestSessionId: string }>(
      `${this.baseUrl}/reservations/${reservationId}/guest-session-id`
    );
  }

 getGroupStatus(groupReservationId: string) {
  return this.http.get<GroupStatusResponse>(
    `${this.baseUrl}/guest/groups/${groupReservationId}/status`
  );
}

  submitGroupOrder(groupReservationId: string, reservationId: string) {
    return this.http.post(
      `${this.baseUrl}/group-reservations/${groupReservationId}/submit`,
      { reservationId }
    );
    }
    
    submit$(groupReservationId: string, reservationId: string) {
  return this.submitGroupOrder(groupReservationId, reservationId);
    }
    getSessions$(groupReservationId: string) {
  return this.getGroupSessions(groupReservationId);
}

    getOwnerSessionId$(reservationId: string) {
    return this.getGuestSessionId(reservationId).pipe(
        map(response => response.guestSessionId)
    );
    }

getStatus$(groupReservationId: string) {
  return this.getGroupStatus(groupReservationId);
    }
    getGroupInvite$(reservationId: string) {
  return this.getInviteToken(reservationId).pipe(
    switchMap(response => this.getInvite(response.token))
  );
}
}
