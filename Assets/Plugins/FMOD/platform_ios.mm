#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>

void (*gSuspendCallback)(bool suspend);

extern "C" void RegisterSuspendCallback(void (*callback)(bool))
{
    if (!gSuspendCallback)
    {
        gSuspendCallback = callback;
        
        [[NSNotificationCenter defaultCenter] addObserverForName:AVAudioSessionInterruptionNotification object:nil queue:nil usingBlock:^(NSNotification *notification)
        {
              bool began = [[notification.userInfo valueForKey:AVAudioSessionInterruptionTypeKey] intValue] == AVAudioSessionInterruptionTypeBegan;
              
              if (!began)
              {
                  [[AVAudioSession sharedInstance] setActive:TRUE error:nil];
              }
              
              if (gSuspendCallback)
              {
                  gSuspendCallback(began);
              }
          }];
    }
}
