#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>

void (*gSuspendCallback)(bool suspend);
bool gIsSuspended = false;

extern "C" void RegisterSuspendCallback(void (*callback)(bool))
{
    if (!gSuspendCallback)
    {
        gSuspendCallback = callback;

        [[NSNotificationCenter defaultCenter] addObserverForName:AVAudioSessionInterruptionNotification object:nil queue:nil usingBlock:^(NSNotification *notification)
        {
            bool began = [[notification.userInfo valueForKey:AVAudioSessionInterruptionTypeKey] intValue] == AVAudioSessionInterruptionTypeBegan;
            if (began == gIsSuspended)
            {
                return;
            }

            if (began)
            {
                // Starting in iOS 10, if the system suspended the app process and deactivated the audio session
                // then we get a delayed interruption notification when the app is re-activated. Just ignore that here.
                if (@available(iOS 14.5, *))
                {
                    if ([[notification.userInfo valueForKey:AVAudioSessionInterruptionReasonKey] intValue] == AVAudioSessionInterruptionReasonAppWasSuspended)
                    {
                        return;
                    }
                }
                else if (@available(iOS 10.3, *))
                {
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
                    if ([[notification.userInfo valueForKey:AVAudioSessionInterruptionWasSuspendedKey] boolValue])
                    {
                        return;
                    }
#pragma clang diagnostic pop
                }
            }

            gIsSuspended = began;
            if (!began)
            {
                [[AVAudioSession sharedInstance] setActive:TRUE error:nil];
            }
            if (gSuspendCallback)
            {
                gSuspendCallback(began);
            }
        }];
        
        [[NSNotificationCenter defaultCenter] addObserverForName:UIApplicationDidBecomeActiveNotification object:nil queue:nil usingBlock:^(NSNotification *notification)
        {
#ifndef TARGET_OS_TV
            if (!gIsSuspended)
            {
                return;
            }
#else
            if (gSuspendCallback)
            {
                gSuspendCallback(true);
            }
#endif
            NSError *errorMessage;
            if(![[AVAudioSession sharedInstance] setActive:TRUE error:&errorMessage])
            {
                NSLog(@"UIApplicationDidBecomeActiveNotification: AVAudioSession.setActive() failed: %@", errorMessage);
                return;
            }
            if (gSuspendCallback)
            {
                gSuspendCallback(false);
            }
            gIsSuspended = false;
        }];
    }
}
