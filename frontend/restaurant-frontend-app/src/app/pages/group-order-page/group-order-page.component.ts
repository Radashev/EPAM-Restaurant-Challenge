
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';

import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';

@Component({
    selector: 'app-group-order-page',
    standalone: true,
    imports: [ParticipantCardComponent, CurrencyPipe, DatePipe, ModalComponent],
    templateUrl: './group-order-page.component.html',
    styleUrl: './group-order-page.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
    private readonly groupOrdersService = inject(GroupOrdersService);
    private readonly route = inject(ActivatedRoute);

    readonly invite = signal(GROUP_ORDER_PREVIEW.invite);
    readonly sessions = signal(GROUP_ORDER_PREVIEW.sessions);

    readonly participants = signal(
        GROUP_ORDER_PREVIEW.sessions.participants
    );

    readonly ownerSessionId = signal<string | null>(null);
    readonly groupStatus = signal<string>('Open');
    readonly showSubmitConfirmation = signal(false);
    readonly loadError = signal<string | null>(null);
    readonly isLoading = signal(false);
    readonly isSubmitting = signal(false);
    readonly confirmedTotal = computed(() =>
    this.participants()
        .filter((person) => person.isSubmitted)
        .reduce((total, person) => total + person.total, 0)
    );
    readonly pendingCount = computed(
    () => this.participants().filter(person => !person.isSubmitted).length
    );
    readonly consolidatedOrder = computed(() =>
    consolidatePreOrder(this.participants())
    );
    
    

ngOnInit(): void {
    this.loadGroupOrder();
}

loadGroupOrder(): void {
    const reservationId =
        this.route.snapshot.paramMap.get('reservationId');

    if (!reservationId) {
        this.loadError.set('Group reservation not found');
        return;
    }

    this.loadError.set(null);
    this.isLoading.set(true);

    this.groupOrdersService.getOwnerSessionId$(reservationId).subscribe({
        next: (guestSessionId) => {
            this.ownerSessionId.set(guestSessionId);
        },
        error: (error) => {
            console.error('Failed to load owner session ID:', error);
        },
    });

    this.groupOrdersService.getGroupInvite$(reservationId).subscribe({
        next: (invite) => {
            this.invite.set(invite);
            this.isLoading.set(false);

            const groupId = invite.GroupReservationId;

            this.groupOrdersService.getStatus$(groupId).subscribe({
                next: (status) => {
                    this.groupStatus.set(status.status);
                },
                error: (error) => {
                    console.error('Failed to load group status:', error);
                },
            });

            this.groupOrdersService.getSessions$(groupId).subscribe({
                next: (sessions) => {
                    this.sessions.set(sessions);
                    this.participants.set(sessions.participants);
                },
                error: (error) => {
                    console.error('Failed to load group sessions:', error);
                },
            });
        },
        error: (error) => {
            this.isLoading.set(false);
            this.loadError.set(
                error.status === 404
                    ? 'Group reservation not found'
                    : 'Failed to load group order'
            );
        },
    });
}




    openSubmitConfirmation(): void {
        if (this.groupStatus() !== 'Open') {
            return;
        }

        this.showSubmitConfirmation.set(true);
        }

        cancelSubmitConfirmation(): void {
        this.showSubmitConfirmation.set(false);
        }
    
    
submitGroupOrder(): void {
    if (this.isSubmitting()) {
        return;
    }

    const reservationId =
        this.route.snapshot.paramMap.get('reservationId');

    if (!reservationId || this.groupStatus() !== 'Open') {
        return;
    }

    const confirmed = this.participants().filter(
        (person) => person.isSubmitted
    );

    if (confirmed.length === 0) {
        return;
    }

    const groupReservationId = this.invite().GroupReservationId;

    this.isSubmitting.set(true);

    this.groupOrdersService
        .submit$(groupReservationId, reservationId)
        .subscribe({
            next: () => {
                this.groupStatus.set('Submitted');
                this.showSubmitConfirmation.set(false);
                this.isSubmitting.set(false);
            },
            error: (error) => {
                this.isSubmitting.set(false);
                console.error('Failed to submit group order:', error);
            },
        });
}

}